using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models;

namespace TipsyBaboon.SqlServer.Data
{
    /// <summary>
    /// SQL Server implementation of SchemaSyncServiceBase for database schema synchronization.
    /// Compares SchemaDefinitions from ModelRegistry against live database metadata and applies CREATE/ALTER operations.
    /// Uses hash-based change detection to minimize unnecessary schema modifications.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Phased schema initialization: starter tables (RADModule, ModelRecord) → module records → full schema sync → config invariants → seed data
    /// - Pinpoint sync: Compares SchemaDefinition hash against ModelRecord.SchemaHash to detect drift
    /// - Schema comparison: Detects missing tables, column mismatches, nullable changes, type changes
    /// - ALTER operations: Adds missing columns, handles nullable→non-nullable conversions with defaults
    /// - View registration: Calls IViewModelStore.RegisterView for all types with ViewDefinitionAttribute
    /// - Startup-safe: Creates starter tables synchronously before RADModule references
    /// 
    /// Schema sync is driven by ModelRegistry metadata (SchemaDefinitions), NOT by reflection/attributes.
    /// This avoids scanning-related coupling and ensures SqlServer layer remains pure ADO.NET.
    /// </remarks>
    public class TipsyBaboonSchemaSyncService : SchemaSyncServiceBase
    {
        private readonly IConfiguration _config;
        private readonly IViewModelStore? _viewModelStore;

        public TipsyBaboonSchemaSyncService(
            IConfiguration config,
            IViewModelStore? viewModelStore = null)
        {
            _config = config;
            _viewModelStore = viewModelStore;
        }

        private SqlConnection CreateConnection(string moduleName)
        {
            var conn = ModelRegistry.GetConnectionString(moduleName)
                       ?? _config.GetConnectionString("TipsyBaboonCore");

            if (string.IsNullOrEmpty(conn))
            {
                throw new InvalidOperationException($"No connection string configured for module '{moduleName}'");
            }

            return new SqlConnection(conn);
        }

        public override async Task EnsureCoreSchemaAsync(CancellationToken cancellationToken = default)
        {
            var registeredModules = ModelRegistry.GetAllModules();
            if (registeredModules.Count == 0)
            {
                return;
            }

            ModelRegistry.DiscoverModelTypes(AppDomain.CurrentDomain.GetAssemblies());

            // Phase 1: Create Governance module's starter tables first (RADModule, ModelRecord)
            // This ensures the RADModule table exists before we try to insert records for all modules
            var governanceModule = registeredModules.FirstOrDefault(m => m.Name == "Governance");
            if (governanceModule != null)
            {
                await EnsureStarterTablesAsync(governanceModule.ConnectionString!, cancellationToken);
            }
            
            // Phase 2: Create RADModule records for ALL registered modules
            if (governanceModule != null)
            {
                await using var connection = new SqlConnection(governanceModule.ConnectionString!);
                await connection.OpenAsync(cancellationToken);
                
                foreach (var registeredModule in registeredModules)
                {
                    await EnsureModuleRecordAsync(connection, registeredModule.Name, cancellationToken);
                }
            }

            // Phase 3: Sync schemas for each module
            foreach (var module in registeredModules)
            {
                await EnsureModuleSchemaAsync(module.Name, module.ConnectionString!, cancellationToken);
            }
            
            // Phase 3.5: Register ModelRecords for ALL modules in Governance database
            if (governanceModule != null)
            {
                await using var connection = new SqlConnection(governanceModule.ConnectionString!);
                await connection.OpenAsync(cancellationToken);
                
                var modelRecordTableExists = await TableExistsAsync(connection, "dbo", "ModelRecord", cancellationToken);
                if (modelRecordTableExists)
                {
                    foreach (var module in registeredModules)
                    {
                        var moduleId = await GetModuleIdAsync(connection, module.Name, cancellationToken);
                        var moduleDefinitions = GetSchemaDefinitionsForSync()
                            .Where(d => string.Equals(d.ModuleName, module.Name, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        foreach (var definition in moduleDefinitions)
                        {
                            await UpsertModelRecordForTableAsync(connection, definition, moduleId, cancellationToken);
                        }
                    }
                }
            }
            
            // Phase 4: Upsert ALL config invariants from ALL modules into ConfigItem table
            if (governanceModule != null)
            {
                await using var connection = new SqlConnection(governanceModule.ConnectionString!);
                await connection.OpenAsync(cancellationToken);
                
                var configItemTableExists = await TableExistsAsync(connection, "dbo", "ConfigItem", cancellationToken);
                if (configItemTableExists)
                {
                    foreach (var module in registeredModules)
                    {
                        await UpsertInvariantConfigItemsForModuleAsync(connection, module.Name, cancellationToken);
                        await UpsertSeedConfigItemsForModuleAsync(connection, module.Name, cancellationToken);
                    }
                }
            }
            
            // Phase 5: Process permission requests (seed and invariant)
            if (governanceModule != null)
            {
                await using var connection = new SqlConnection(governanceModule.ConnectionString!);
                await connection.OpenAsync(cancellationToken);
                
                var permissionTableExists = await TableExistsAsync(connection, "dbo", "Permission", cancellationToken);
                if (permissionTableExists)
                {
                    await ProcessPermissionBatchAsync(connection, ModelRegistry.GetSeedPermissions(), isInvariant: false, cancellationToken);
                    await ProcessPermissionBatchAsync(connection, ModelRegistry.GetInvariantPermissions(), isInvariant: true, cancellationToken);
                }
            }
            
            // Phase 6: Re-register views now that all tables exist
            // Views may have failed during initial registration if referenced tables didn't exist yet
            EnsureViewsRegistered();
        }
        
        private async Task EnsureStarterTablesAsync(string connectionString, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var governanceDefinitions = GetSchemaDefinitionsForSync()
                .Where(d => string.Equals(d.ModuleName, "Governance", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            var radModuleDef = governanceDefinitions.FirstOrDefault(d => d.TableName == "RADModule");
            var modelRecordDef = governanceDefinitions.FirstOrDefault(d => d.TableName == "ModelRecord");
            
            // Create/sync RADModule table first
            if (radModuleDef != null)
            {
                var tableExists = await TableExistsAsync(connection, "dbo", "RADModule", cancellationToken);
                if (!tableExists)
                {
                    var ddl = GenerateCreateTableDdl(radModuleDef);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = ddl;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    // Table exists - sync columns to ensure CreatedByDisplayName/ModifiedByDisplayName exist
                    var storedHash = await GetStoredHashAsync(connection, "RADModule", cancellationToken);
                    if (storedHash != radModuleDef.VersionHash)
                    {
                        await SyncColumnsAsync(connection, radModuleDef, cancellationToken);
                    }
                }
            }
            
            // Create/sync ModelRecord table second
            if (modelRecordDef != null)
            {
                var tableExists = await TableExistsAsync(connection, "dbo", "ModelRecord", cancellationToken);
                if (!tableExists)
                {
                    var ddl = GenerateCreateTableDdl(modelRecordDef);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = ddl;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    // Table exists - sync columns to ensure CreatedByDisplayName/ModifiedByDisplayName exist
                    var storedHash = await GetStoredHashAsync(connection, "ModelRecord", cancellationToken);
                    if (storedHash != modelRecordDef.VersionHash)
                    {
                        await SyncColumnsAsync(connection, modelRecordDef, cancellationToken);
                    }
                }
            }
        }

        private void EnsureViewsRegistered()
        {
            if (_viewModelStore == null)
                return;

            var viewTypes = ModelRegistry.GetViewModelTypes();

            foreach (var type in viewTypes)
            {
                var prop = type.GetProperty("ViewDefinition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null || !typeof(Core.Data.ViewDefinition).IsAssignableFrom(prop.PropertyType))
                {
                    throw new InvalidOperationException($"View model {type.FullName} must declare a public static property 'ViewDefinition' of type ViewDefinition.");
                }

                var viewDef = (Core.Data.ViewDefinition)prop.GetValue(null)!;

                var registerMethod = _viewModelStore.GetType().GetMethod("RegisterView");
                if (registerMethod == null)
                    throw new InvalidOperationException($"ViewModelStore does not have a RegisterView method.");

                var generic = registerMethod.MakeGenericMethod(type);
                generic.Invoke(_viewModelStore, new object[] { viewDef });
            }
        }

        private async Task EnsureModuleSchemaAsync(string moduleName, string connectionString, CancellationToken cancellationToken)
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var moduleDefinitions = GetSchemaDefinitionsForSync()
                .Where(d => string.Equals(d.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!moduleDefinitions.Any())
            {
                return;
            }

            // Phase 1: Ensure starter tables exist (RADModule, then ModelRecord) - Governance module only
            var radModuleDef = moduleDefinitions.FirstOrDefault(d => d.TableName == "RADModule");
            var modelRecordDef = moduleDefinitions.FirstOrDefault(d => d.TableName == "ModelRecord");
            var starterTables = new[] { radModuleDef, modelRecordDef }.Where(d => d != null).ToList();
            var regularTables = moduleDefinitions.Where(d => d.TableName != "RADModule" && d.TableName != "ModelRecord").ToList();
            
            // Create starter tables first (RADModule before ModelRecord for FK consistency)
            foreach (var definition in starterTables)
            {
                if (definition == null) continue;
                await EnsureTableAsync(connection, definition, isStarterTable: true, cancellationToken);
            }
            
            // Phase 2: Process regular tables (create/alter, seeds/invariants)
            // Note: ModelRecord registration happens centrally in Phase 3.5 of EnsureCoreSchemaAsync
            foreach (var definition in regularTables)
            {
                await SyncTableAsync(connection, definition, cancellationToken);
            }
            
            // Phase 3: Ensure indexes for all tables
            foreach (var definition in moduleDefinitions)
            {
                await EnsureIndexesAsync(connection, definition, cancellationToken);
            }
            
            // Phase 4: Ensure foreign key constraints for all tables
            foreach (var definition in moduleDefinitions)
            {
                await EnsureForeignKeysAsync(connection, definition, cancellationToken);
            }
        }
        
        private async Task EnsureTableAsync(SqlConnection connection, SchemaDefinition definition, bool isStarterTable, CancellationToken cancellationToken)
        {
            var tableExists = await TableExistsAsync(connection, "dbo", definition.TableName, cancellationToken);
            
            if (!tableExists)
            {
                var ddl = GenerateCreateTableDdl(definition);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                
                // Insert seed records for newly created tables
                await InsertSeedRecordsAsync(connection, definition, cancellationToken);
            }
            else if (!isStarterTable)
            {
                // For existing non-starter tables, check version and sync columns if needed
                var storedHash = await GetStoredHashAsync(connection, definition.TableName, cancellationToken);
                if (storedHash != definition.VersionHash)
                {
                    await SyncColumnsAsync(connection, definition, cancellationToken);
                }
            }
            
            // Upsert invariant records
            await UpsertInvariantRecordsAsync(connection, definition, cancellationToken);
        }
        
        private async Task SyncTableAsync(SqlConnection connection, SchemaDefinition definition, CancellationToken cancellationToken)
        {
            var tableExists = await TableExistsAsync(connection, "dbo", definition.TableName, cancellationToken);
            var isNewTable = !tableExists;
            
            if (!tableExists)
            {
                // Create table
                var ddl = GenerateCreateTableDdl(definition);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                // Check version and sync columns if needed
                var storedHash = await GetStoredHashAsync(connection, definition.TableName, cancellationToken);
                if (storedHash != definition.VersionHash)
                {
                    await SyncColumnsAsync(connection, definition, cancellationToken);
                }
            }
            
            // Insert seed records only for new tables
            if (isNewTable)
            {
                await InsertSeedRecordsAsync(connection, definition, cancellationToken);
            }
            
            // Upsert invariant records
            await UpsertInvariantRecordsAsync(connection, definition, cancellationToken);
        }
        
        private async Task UpsertModelRecordForTableAsync(SqlConnection connection, SchemaDefinition definition, Guid moduleId, CancellationToken cancellationToken)
        {
            // Check if ModelRecord table exists
            var modelRecordTableExists = await TableExistsAsync(connection, "dbo", "ModelRecord", cancellationToken);
            if (!modelRecordTableExists)
            {
                throw new InvalidOperationException(
                    $"Cannot register model '{definition.TypeName}' for module '{definition.ModuleName}': " +
                    "ModelRecord table does not exist in Governance database. Schema sync should have created it.");
            }
            
            // Generate deterministic Id for this model type
            var modelId = ModelRegistry.GenerateDeterministicId(definition.ModuleName + '|' + definition.TypeName);
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                MERGE INTO [dbo].[ModelRecord] AS target
                USING (SELECT @Id AS Id) AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET
                        Name = @Name,
                        IsInMenu = @IsInMenu,
                        HasForms = @HasForms,
                        HasLocalPermissions = @HasLocalPermissions,
                        TableName = @TableName,
                        AssemblyName = @AssemblyName,
                        FullyQualifiedTypeName = @FullyQualifiedTypeName,
                        VersionHash = @VersionHash,
                        ModuleId = @ModuleId,
                        ModifiedOn = SYSDATETIMEOFFSET(),
                        ModifiedByDisplayName = 'System'
                WHEN NOT MATCHED THEN
                    INSERT (Id, Name, IsInMenu, HasForms, HasLocalPermissions, TableName, AssemblyName, FullyQualifiedTypeName, VersionHash, ModuleId, CreatedOn, CreatedByDisplayName, IsInvariant, OwnershipLevel, OwnerId)
                    VALUES (@Id, @Name, @IsInMenu, @HasForms, @HasLocalPermissions, @TableName, @AssemblyName, @FullyQualifiedTypeName, @VersionHash, @ModuleId, SYSDATETIMEOFFSET(), 'System', 1, 2, NULL);";
            
            cmd.Parameters.AddWithValue("@Id", modelId);
            cmd.Parameters.AddWithValue("@Name", definition.TypeName);
            cmd.Parameters.AddWithValue("@IsInMenu", definition.ShowInMenu);
            cmd.Parameters.AddWithValue("@HasForms", definition.HasForms);
            cmd.Parameters.AddWithValue("@HasLocalPermissions", definition.HasLocalPermissions);
            cmd.Parameters.AddWithValue("@TableName", definition.TableName);
            cmd.Parameters.AddWithValue("@AssemblyName", definition.AssemblyName);
            cmd.Parameters.AddWithValue("@FullyQualifiedTypeName", definition.FullyQualifiedTypeName);
            cmd.Parameters.AddWithValue("@VersionHash", definition.VersionHash);
            cmd.Parameters.AddWithValue("@ModuleId", moduleId);
            
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<string?> GetStoredHashAsync(
            Microsoft.Data.SqlClient.SqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            if (!await TableExistsAsync(connection, "dbo", "ModelRecord", cancellationToken))
                return null;

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT VersionHash FROM [dbo].[ModelRecord] WHERE TableName = @TableName";
                cmd.Parameters.AddWithValue("@TableName", tableName);

                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                return result as string;
            }
            catch
            {
                return null;
            }
        }

        private async Task<int> SyncColumnsAsync(
            Microsoft.Data.SqlClient.SqlConnection connection,
            SchemaDefinition definition,
            CancellationToken cancellationToken)
        {
            var dbColumns = await GetTableColumnsAsync(connection, "dbo", definition.TableName, cancellationToken);
            var dbColumnMap = dbColumns.ToDictionary(c => c.Name.ToLowerInvariant(), c => c);

            int changes = 0;

            foreach (var col in definition.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
            {
                var colNameLower = col.ColumnName.ToLowerInvariant();
                var sqlType = GetSqlTypeFromColumnDef(col);
                var nullable = col.IsNullable ? "NULL" : "NOT NULL";

                if (!dbColumnMap.TryGetValue(colNameLower, out var dbCol))
                {
                    try
                    {
                        using var cmd = connection.CreateCommand();
                        
                        // Get default value for non-nullable, non-generated columns
                        var defaultValue = GetDefaultValueForProperty(definition, col);
                        var defaultConstraint = defaultValue != null ? $" DEFAULT {defaultValue}" : "";
                        
                        // For primary keys, always NOT NULL
                        if (col.IsPrimaryKey)
                        {
                            cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ADD [{col.ColumnName}] {sqlType} NOT NULL";
                        }
                        // For non-nullable columns with existing data, add as NULL first, update, then make NOT NULL with DEFAULT
                        else if (!col.IsNullable)
                        {
                            // Add column as NULL first
                            cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ADD [{col.ColumnName}] {sqlType} NULL";
                            await cmd.ExecuteNonQueryAsync(cancellationToken);
                            
                            // Set default value for existing rows
                            if (defaultValue != null)
                            {
                                cmd.CommandText = $"UPDATE [dbo].[{definition.TableName}] SET [{col.ColumnName}] = {defaultValue} WHERE [{col.ColumnName}] IS NULL";
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }
                            
                            // Now make it NOT NULL with DEFAULT constraint
                            cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ALTER COLUMN [{col.ColumnName}] {sqlType} NOT NULL";
                            await cmd.ExecuteNonQueryAsync(cancellationToken);
                            
                            // Add DEFAULT constraint for future inserts
                            if (defaultValue != null)
                            {
                                cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ADD CONSTRAINT [DF_{definition.TableName}_{col.ColumnName}] DEFAULT {defaultValue} FOR [{col.ColumnName}]";
                            }
                        }
                        else
                        {
                            // Nullable column - just add it
                            cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ADD [{col.ColumnName}] {sqlType} NULL";
                        }
                        
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                        changes++;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to add column [{col.ColumnName}] to table [{definition.TableName}]", ex);
                    }
                }
                else
                {
                    var alterNeeded = false;
                    var alterReason = "";

                    if (col.MaxLength > 0 && dbCol.MaxLength > 0 && col.MaxLength > dbCol.MaxLength)
                    {
                        alterNeeded = true;
                        alterReason = $"length {dbCol.MaxLength} → {col.MaxLength}";
                    }
                    else if (col.MaxLength <= 0 && dbCol.MaxLength > 0 && (Nullable.GetUnderlyingType(col.ClrType) ?? col.ClrType) == typeof(string))
                    {
                        alterNeeded = true;
                        alterReason = $"length {dbCol.MaxLength} → MAX";
                    }

                    if (alterNeeded)
                    {
                        try
                        {
                            using var cmd = connection.CreateCommand();
                            cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ALTER COLUMN [{col.ColumnName}] {sqlType} {nullable}";
                            await cmd.ExecuteNonQueryAsync(cancellationToken);
                            changes++;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Failed to alter column [{col.ColumnName}] on table [{definition.TableName}]", ex);
                        }
                    }
                }
            }

            return changes;
        }

        private static string? GetDefaultValueForProperty(SchemaDefinition definition, ColumnDefinition col)
        {
            // Skip if nullable or database-generated
            if (col.IsNullable || col.DatabaseGenerated == DatabaseGeneratedOption.Identity || col.DatabaseGenerated == DatabaseGeneratedOption.Computed)
                return null;
                
            // Skip primary keys
            if (col.IsPrimaryKey)
                return null;

            try
            {
                // Create instance to read default value
                var instance = Activator.CreateInstance(definition.ModelType);
                if (instance == null)
                    return null;
                    
                var prop = definition.ModelType.GetProperty(col.PropertyName);
                if (prop == null)
                    return null;
                    
                var value = prop.GetValue(instance);
                return ConvertToSqlLiteral(value, col.ClrType);
            }
            catch
            {
                // If we can't instantiate or read the property, fall back to type-based default
                return GetDefaultValueForType(col.ClrType);
            }
        }
        
        private static string ConvertToSqlLiteral(object? value, Type type)
        {
            if (value == null)
                return "NULL";
                
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            
            if (underlying == typeof(string))
                return $"'{value.ToString()?.Replace("'", "''")}'"; // SQL escape single quotes
                
            if (underlying == typeof(bool))
                return (bool)value ? "1" : "0";
                
            if (underlying.IsEnum)
                return Convert.ToInt32(value).ToString();
                
            if (underlying == typeof(DateTime))
            {
                var dt = (DateTime)value;
                return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
            }
            
            if (underlying == typeof(DateTimeOffset))
            {
                var dto = (DateTimeOffset)value;
                return $"'{dto:yyyy-MM-dd HH:mm:ss zzz}'";
            }
            
            if (underlying == typeof(Guid))
                return $"'{value}'";
                
            if (underlying == typeof(TimeSpan))
            {
                var ts = (TimeSpan)value;
                return $"'{ts:hh\\:mm\\:ss}'";
            }
            
            // Numeric types (int, long, decimal, double, float, etc.)
            return value.ToString() ?? "0";
        }

        private static string GetDefaultValueForType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string)) return "''";
            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) || underlying == typeof(byte)) return "0";
            if (underlying == typeof(bool)) return "0";
            if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float)) return "0";
            if (underlying == typeof(DateTime)) return "'1900-01-01'";
            if (underlying == typeof(DateTimeOffset)) return "'1900-01-01 00:00:00 +00:00'";
            if (underlying == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
            if (underlying == typeof(TimeSpan)) return "'00:00:00'";
            
            if (underlying.IsEnum)
                return "0";

            return "NULL";
        }

        private static string GetSqlTypeFromColumnDef(ColumnDefinition col)
        {
            var underlying = Nullable.GetUnderlyingType(col.ClrType) ?? col.ClrType;

            if (underlying == typeof(string))
            {
                var prefix = col.IsUnicode ? "nvarchar" : "varchar";
                return col.MaxLength > 0 ? $"{prefix}({col.MaxLength})" : $"{prefix}(max)";
            }
            if (underlying == typeof(int)) return "int";
            if (underlying == typeof(long)) return "bigint";
            if (underlying == typeof(short)) return "smallint";
            if (underlying == typeof(byte)) return "tinyint";
            if (underlying == typeof(bool)) return "bit";
            if (underlying == typeof(decimal)) return $"decimal({col.Precision},{col.Scale})";
            if (underlying == typeof(double)) return "float";
            if (underlying == typeof(float)) return "real";
            if (underlying == typeof(DateTime)) return "datetime2";
            if (underlying == typeof(DateTimeOffset)) return "datetimeoffset";
            if (underlying == typeof(Guid)) return "uniqueidentifier";
            if (underlying == typeof(byte[])) return "varbinary(max)";
            if (underlying == typeof(TimeSpan)) return "time";
            if (underlying.IsEnum) return "int";

            return col.IsUnicode ? "nvarchar(max)" : "varchar(max)";
        }

        public override async Task<SchemaSyncResult> EnsureSchemaForAssembliesAsync(
            IEnumerable<Assembly> assemblies,
            CancellationToken cancellationToken = default)
        {
            ModelRegistry.DiscoverModelTypes(assemblies);
            var allDefinitions = GetSchemaDefinitionsForSync();

            var registeredModules = ModelRegistry.GetAllModules().Select(m => m.Name).ToHashSet();
            var filteredDefinitions = allDefinitions.Where(d => registeredModules.Contains(d.ModuleName));

            var skippedModules = allDefinitions
                .Select(d => d.ModuleName)
                .Distinct()
                .Where(m => !registeredModules.Contains(m))
                .ToList();

            if (skippedModules.Any())
            {

            }

            return await EnsureSchemaAsync(filteredDefinitions, cancellationToken);
        }

        public override async Task<SchemaSyncResult> EnsureSchemaAsync(
            IEnumerable<SchemaDefinition> definitions,
            CancellationToken cancellationToken = default)
        {
            var result = new SchemaSyncResult { Success = true };

            var registeredModules = ModelRegistry.GetAllModules().Select(m => m.Name).ToHashSet();
            var filteredDefinitions = definitions.Where(d => registeredModules.Contains(d.ModuleName));

            var byModule = filteredDefinitions
                .GroupBy(d => d.ModuleName)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (moduleName, moduleDefs) in byModule)
            {
                var moduleResult = await EnsureSchemaForDefinitionsAsync(moduleName, moduleDefs, cancellationToken);
                result.TablesCreated += moduleResult.TablesCreated;
                result.ColumnsAdded += moduleResult.ColumnsAdded;
                result.ColumnsModified += moduleResult.ColumnsModified;
                result.AppliedChanges.AddRange(moduleResult.AppliedChanges);
                result.Warnings.AddRange(moduleResult.Warnings);
                result.Errors.AddRange(moduleResult.Errors);
                if (!moduleResult.Success) result.Success = false;
            }

            return result;
        }

        private async Task<SchemaSyncResult> EnsureSchemaForDefinitionsAsync(
            string moduleName,
            IEnumerable<SchemaDefinition> definitions,
            CancellationToken cancellationToken = default)
        {
            var result = new SchemaSyncResult { Success = true };

            await using var connection = CreateConnection(moduleName);
            await connection.OpenAsync(cancellationToken);

            try
            {
                foreach (var definition in definitions)
                {
                    var tableName = definition.TableName;
                    var tableExists = await TableExistsAsync(connection, "dbo", tableName, cancellationToken);

                    if (!tableExists)
                    {
                        var ddl = GenerateCreateTableDdl(definition);

                        try
                        {
                            using var cmd = connection.CreateCommand();
                            cmd.CommandText = ddl;
                            await cmd.ExecuteNonQueryAsync(cancellationToken);

                            result.TablesCreated++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Failed to create table {tableName}: {ex.Message}");
                            result.Success = false;

                        }
                    }
                    else
                    {
                        var columnResult = await SyncColumnsForDefinitionAsync(connection, definition, cancellationToken);
                        result.ColumnsAdded += columnResult.ColumnsAdded;
                        result.ColumnsModified += columnResult.ColumnsModified;
                        result.AppliedChanges.AddRange(columnResult.AppliedChanges);
                        result.Warnings.AddRange(columnResult.Warnings);
                    }
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            return result;
        }

        private async Task<SchemaSyncResult> SyncColumnsForDefinitionAsync(
            System.Data.Common.DbConnection connection,
            SchemaDefinition definition,
            CancellationToken cancellationToken)
        {
            var result = new SchemaSyncResult { Success = true };
            var dbColumns = await GetTableColumnsAsync(connection, "dbo", definition.TableName, cancellationToken);

            foreach (var col in definition.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
            {
                var dbColumn = dbColumns.FirstOrDefault(c =>
                    c.Name.Equals(col.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (dbColumn == null)
                {
                    var sqlType = GetSqlTypeFromColumnDef(col);
                    var defaultValue = GetDefaultValueForProperty(definition, col);
                    var defaultConstraint = defaultValue != null ? $" DEFAULT {defaultValue}" : "";

                    try
                    {
                        using var cmd = connection.CreateCommand();
                        var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                        cmd.CommandText = $"ALTER TABLE [dbo].[{definition.TableName}] ADD [{col.ColumnName}] {sqlType} {nullable}{defaultConstraint}";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);

                        result.ColumnsAdded++;

                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to add column [{col.ColumnName}] to table [{definition.TableName}]: {ex.Message}", ex);
                    }
                }
            }

            return result;
        }

        private string GenerateCreateTableDdl(SchemaDefinition definition)
        {
            var columns = new List<string>();
            var keyColumns = definition.Columns
                .Where(c => !c.IsNavigationProperty && !c.IsNotMapped && c.IsPrimaryKey)
                .OrderBy(c => c.KeyOrdinal ?? 0)
                .ToList();

            foreach (var col in definition.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
            {
                var sqlType = GetSqlTypeFromColumnDef(col);
                var nullability = col.IsNullable ? "NULL" : "NOT NULL";
                var identity = col.DatabaseGenerated == DatabaseGeneratedOption.Identity ? " IDENTITY(1,1)" : "";
                
                // Add DEFAULT constraint for non-nullable, non-generated columns
                var defaultConstraint = "";
                if (!col.IsPrimaryKey)
                {
                    var defaultValue = GetDefaultValueForProperty(definition, col);
                    if (defaultValue != null)
                        defaultConstraint = $" DEFAULT {defaultValue}";
                }

                if (col.IsPrimaryKey && keyColumns.Count == 1)
                {
                    columns.Add($"    [{col.ColumnName}] {sqlType}{identity} NOT NULL PRIMARY KEY");
                }
                else
                {
                    var notNull = col.IsPrimaryKey ? "NOT NULL" : nullability;
                    columns.Add($"    [{col.ColumnName}] {sqlType}{identity} {notNull}{defaultConstraint}");
                }
            }

            if (keyColumns.Count > 1)
            {
                var keyColumnList = string.Join(", ", keyColumns.Select(c => $"[{c.ColumnName}]"));
                columns.Add($"    CONSTRAINT [PK_{definition.TableName}] PRIMARY KEY ({keyColumnList})");
            }

            return $"CREATE TABLE [dbo].[{definition.TableName}] (\n{string.Join(",\n", columns)}\n);";
        }

        private async Task EnsureIndexesAsync(SqlConnection connection, SchemaDefinition definition, CancellationToken cancellationToken)
        {
            if (definition.Indexes == null || definition.Indexes.Count == 0)
                return;

            foreach (var index in definition.Indexes)
            {
                var indexExists = await SysObjectExistsAsync(connection, "sys.indexes", "name", index.IndexName, cancellationToken);
                if (indexExists)
                    continue;

                var uniqueClause = index.IsUnique ? "UNIQUE " : "";
                var clusteredClause = index.IsClustered ? "CLUSTERED " : "NONCLUSTERED ";
                var columnList = string.Join(", ", index.ColumnNames.Select(c => $"[{c}]"));

                var ddl = $"CREATE {uniqueClause}{clusteredClause}INDEX [{index.IndexName}] ON [dbo].[{definition.TableName}] ({columnList});";

                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private async Task<bool> SysObjectExistsAsync(SqlConnection connection, string catalogTable, string columnName, string objectName, CancellationToken cancellationToken)
        {
            var sql = $"SELECT COUNT(*) FROM {catalogTable} WHERE [{columnName}] = @Name";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@Name", objectName);
            var count = (int)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        private async Task EnsureForeignKeysAsync(SqlConnection connection, SchemaDefinition definition, CancellationToken cancellationToken)
        {
            var fkColumns = definition.Columns.Where(c => c.IsForeignKey && !string.IsNullOrEmpty(c.ForeignKeyTarget)).ToList();
            if (fkColumns.Count == 0)
                return;

            var allSchemas = GetSchemaDefinitionsForSync();

            foreach (var fkCol in fkColumns)
            {
                var fkName = $"FK_{definition.TableName}_{fkCol.ColumnName}";
                
                var fkExists = await SysObjectExistsAsync(connection, "sys.foreign_keys", "name", fkName, cancellationToken);
                if (fkExists)
                    continue;

                var referencedTableName = fkCol.ForeignKeyTarget!;
                var referencedSchema = allSchemas.FirstOrDefault(s => 
                    string.Equals(s.TypeName, referencedTableName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.TableName, referencedTableName, StringComparison.OrdinalIgnoreCase));

                if (referencedSchema == null)
                    continue;

                // Skip cross-module foreign keys - each module should be in its own database
                // Only create FK if the referenced table is in the same module
                if (!string.Equals(definition.ModuleName, referencedSchema.ModuleName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var referencedTableExists = await TableExistsAsync(connection, "dbo", referencedSchema.TableName, cancellationToken);
                if (!referencedTableExists)
                    continue;

                var referencedPkColumn = referencedSchema.KeyColumns.FirstOrDefault();
                if (referencedPkColumn == null)
                    continue;

                var cascadeClause = fkCol.CascadeOnDelete ? " ON DELETE CASCADE" : "";
                var ddl = $@"ALTER TABLE [dbo].[{definition.TableName}] 
                    ADD CONSTRAINT [{fkName}] 
                    FOREIGN KEY ([{fkCol.ColumnName}]) 
                    REFERENCES [dbo].[{referencedSchema.TableName}] ([{referencedPkColumn.ColumnName}]){cascadeClause}";

                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = ddl;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create FK constraint [{fkName}] on table [{definition.TableName}]", ex);
                }
            }
        }

        private async Task InsertSeedRecordsAsync(SqlConnection connection, SchemaDefinition definition, CancellationToken cancellationToken)
        {
            var seedRecords = definition.SeedRecords ?? new List<object>();

            var manualSeeds = ModelRegistry.GetAllManualSeeds()
                .Where(s => s.GetType() == definition.ModelType)
                .ToList();
            seedRecords = seedRecords.Concat(manualSeeds).ToList();

            if (seedRecords.Count == 0)
                return;

            foreach (var record in seedRecords)
            {
                await InsertRecordAsync(connection, definition, record, cancellationToken);
            }
        }

        private async Task UpsertInvariantRecordsAsync(SqlConnection connection, SchemaDefinition definition, CancellationToken cancellationToken)
        {
            var invariantRecords = definition.InvariantRecords ?? new List<object>();

            var manualInvariants = ModelRegistry.GetAllManualInvariants()
                .Where(i => i.GetType() == definition.ModelType)
                .ToList();
            invariantRecords = invariantRecords.Concat(manualInvariants).ToList();

            foreach (var record in invariantRecords)
            {
                await UpsertRecordAsync(connection, definition, record, cancellationToken);
            }

            await ClearOrphanedInvariantsAsync(connection, definition, invariantRecords, cancellationToken);
        }
        
        private async Task UpsertInvariantConfigItemsForModuleAsync(SqlConnection connection, string moduleName, CancellationToken cancellationToken)
        {
            var invariantConfigs = ModelRegistry.GetManualInvariantConfigsForModule(moduleName);
            if (invariantConfigs.Count == 0)
                return;
            
            // Get the ModuleId for the module that owns these configs
            // All registered modules should have RADModule records by now (created in first pass)
            var moduleId = await GetModuleIdAsync(connection, moduleName, cancellationToken);
            
            foreach (var kvp in invariantConfigs)
            {
                await UpsertInvariantConfigItemAsync(connection, moduleId, kvp.Key, kvp.Value, cancellationToken);
            }
        }

        private async Task UpsertSeedConfigItemsForModuleAsync(SqlConnection connection, string moduleName, CancellationToken cancellationToken)
        {
            var invariantConfigs = ModelRegistry.GetManualSeedConfigsForModule(moduleName);
            if (invariantConfigs.Count == 0)
                return;

            // Get the ModuleId for the module that owns these configs
            // All registered modules should have RADModule records by now (created in first pass)
            var moduleId = await GetModuleIdAsync(connection, moduleName, cancellationToken);

            foreach (var kvp in invariantConfigs)
            {
                await UpsertSeedConfigItemAsync(connection, moduleId, kvp.Key, kvp.Value, cancellationToken);
            }
        }


        private async Task EnsureModuleRecordAsync(SqlConnection connection, string moduleName, CancellationToken cancellationToken)
        {
            var registeredModule = ModelRegistry.GetAllModules().FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));
            if (registeredModule == null)
            {
                throw new InvalidOperationException($"Module '{moduleName}' is not registered in ModelRegistry.");
            }

            var moduleTableExists = await TableExistsAsync(connection, "dbo", "RADModule", cancellationToken);
            if (!moduleTableExists)
            {
                throw new InvalidOperationException(
                    $"Cannot register module '{moduleName}': " +
                    "RADModule table does not exist in Governance database. Schema sync should have created it.");
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                MERGE INTO [dbo].[RADModule] AS target
                USING (SELECT @Name AS Name) AS source
                ON target.Name = source.Name
                WHEN MATCHED THEN
                    UPDATE SET
                        Description = @Description,
                        ModifiedOn = SYSDATETIMEOFFSET(),
                        ModifiedByDisplayName = 'System'
                WHEN NOT MATCHED THEN
                    INSERT (Id, Name, Description, CreatedOn, CreatedByDisplayName, IsInvariant, OwnershipLevel, OwnerId)
                    VALUES (NEWID(), @Name, @Description, SYSDATETIMEOFFSET(), 'System', 1, 2, NULL);";;

            cmd.Parameters.AddWithValue("@Name", moduleName);
            cmd.Parameters.AddWithValue("@Description", (object?)registeredModule.Description ?? $"Module: {moduleName}");

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<Guid> GetModuleIdAsync(SqlConnection connection, string moduleName, CancellationToken cancellationToken)
        {
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id FROM dbo.[RADModule] WHERE Name = @Name";
            selectCmd.Parameters.AddWithValue("@Name", moduleName);
            
            var result = await selectCmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                return (Guid)result;
            }
            
            throw new InvalidOperationException($"Module '{moduleName}' not found. This should not happen - EnsureModuleRecordAsync should have created it.");
        }
        
        private async Task UpsertInvariantConfigItemAsync(SqlConnection connection, Guid moduleId, string key, string value, CancellationToken cancellationToken)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                IF EXISTS (SELECT 1 FROM dbo.ConfigItem WHERE ModuleId = @ModuleId AND [Key] = @Key)
                BEGIN
                    UPDATE dbo.ConfigItem
                    SET Value = @Value, IsInvariant = 1, OwnershipLevel = 2, ModifiedOn = SYSDATETIMEOFFSET(), ModifiedByDisplayName = 'System'
                    WHERE ModuleId = @ModuleId AND [Key] = @Key
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.ConfigItem (Id, ModuleId, [Key], Value, ValueType, Precedence, IsRequired, IsInvariant, OwnershipLevel, OwnerId, CreatedOn, CreatedByDisplayName)
                    VALUES (NEWID(), @ModuleId, @Key, @Value, 'System.String', 0, 0, 1, 2, NULL, SYSDATETIMEOFFSET(), 'System')
                END";
            
            cmd.Parameters.AddWithValue("@ModuleId", moduleId);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@Value", value);
            
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task UpsertSeedConfigItemAsync(SqlConnection connection, Guid moduleId, string key, string value, CancellationToken cancellationToken)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                IF EXISTS (SELECT 1 FROM dbo.ConfigItem WHERE ModuleId = @ModuleId AND [Key] = @Key)
                BEGIN
                    UPDATE dbo.ConfigItem
                    SET Value = @Value, IsInvariant = 1, OwnershipLevel = 2, ModifiedOn = SYSDATETIMEOFFSET(), ModifiedByDisplayName = 'System'
                    WHERE ModuleId = @ModuleId AND [Key] = @Key
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.ConfigItem (Id, ModuleId, [Key], Value, ValueType, Precedence, IsRequired, IsInvariant, OwnershipLevel, OwnerId, CreatedOn, CreatedByDisplayName)
                    VALUES (NEWID(), @ModuleId, @Key, @Value, 'System.String', 0, 0, 0, 2, NULL, SYSDATETIMEOFFSET(), 'System')
                END";

            cmd.Parameters.AddWithValue("@ModuleId", moduleId);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@Value", value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task ClearOrphanedInvariantsAsync(SqlConnection connection, SchemaDefinition definition, List<object> currentInvariants, CancellationToken cancellationToken)
        {
            var keyColumns = definition.KeyColumns;
            if (keyColumns.Count == 0)
                return;

            if (!definition.Columns.Any(c => c.PropertyName == "IsInvariant"))
                return;

            if (currentInvariants.Count == 0)
            {
                using var clearAllCmd = connection.CreateCommand();
                clearAllCmd.CommandText = $"UPDATE [dbo].[{definition.TableName}] SET [IsInvariant] = 0 WHERE [IsInvariant] = 1";
                await clearAllCmd.ExecuteNonQueryAsync(cancellationToken);
                return;
            }

            using var cmd = connection.CreateCommand();
            var paramIndex = 0;

            if (keyColumns.Count == 1)
            {
                var keyColumn = keyColumns[0];
                var values = currentInvariants
                    .Select(r => definition.ModelType.GetProperty(keyColumn.PropertyName)?.GetValue(r))
                    .Where(v => v != null)
                    .ToList();

                var paramList = string.Join(", ", values.Select((_, i) => $"@inv{i}"));
                cmd.CommandText = $"UPDATE [dbo].[{definition.TableName}] SET [IsInvariant] = 0 WHERE [IsInvariant] = 1 AND [{keyColumn.ColumnName}] NOT IN ({paramList})";

                for (int i = 0; i < values.Count; i++)
                    cmd.Parameters.AddWithValue($"@inv{i}", values[i]!);
            }
            else
            {
                var conditions = new List<string>();
                foreach (var record in currentInvariants)
                {
                    var parts = new List<string>();
                    foreach (var keyCol in keyColumns)
                    {
                        var value = definition.ModelType.GetProperty(keyCol.PropertyName)?.GetValue(record);
                        var paramName = $"@inv{paramIndex++}";
                        parts.Add($"[{keyCol.ColumnName}] = {paramName}");
                        cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
                    }
                    conditions.Add($"({string.Join(" AND ", parts)})");
                }
                cmd.CommandText = $"UPDATE [dbo].[{definition.TableName}] SET [IsInvariant] = 0 WHERE [IsInvariant] = 1 AND NOT ({string.Join(" OR ", conditions)})";
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task InsertRecordAsync(SqlConnection connection, SchemaDefinition definition, object record, CancellationToken cancellationToken)
        {
            var columns = new List<string>();
            var paramNames = new List<string>();
            var parameters = new List<SqlParameter>();

            foreach (var col in definition.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
            {
                var prop = definition.ModelType.GetProperty(col.PropertyName);
                if (prop == null) continue;

                // Skip database-generated columns (like IDENTITY columns)
                if (prop.GetCustomAttribute<DatabaseGeneratedAttribute>() != null)
                    continue;

                var value = prop.GetValue(record);
                if (value == null && !col.IsNullable) continue;

                columns.Add($"[{col.ColumnName}]");
                var paramName = $"@p{parameters.Count}";
                paramNames.Add(paramName);
                parameters.Add(new SqlParameter(paramName, value ?? DBNull.Value));
            }

            var sql = $"INSERT INTO [dbo].[{definition.TableName}] ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters.ToArray());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task UpsertRecordAsync(SqlConnection connection, SchemaDefinition definition, object record, CancellationToken cancellationToken)
        {
            var keyColumns = definition.KeyColumns;
            if (keyColumns.Count == 0)
                return;

            var columnNames = new List<string>();
            var paramNames = new List<string>();
            var updateClauses = new List<string>();
            var parameters = new List<SqlParameter>();
            var keyConditions = new List<string>();

            foreach (var col in definition.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
            {
                var prop = definition.ModelType.GetProperty(col.PropertyName);
                if (prop == null) continue;

                var value = prop.GetValue(record);
                var paramName = $"@p{parameters.Count}";
                parameters.Add(new SqlParameter(paramName, value ?? DBNull.Value));

                columnNames.Add(col.ColumnName);
                paramNames.Add(paramName);

                if (col.IsPrimaryKey)
                {
                    keyConditions.Add($"[{col.ColumnName}] = {paramName}");
                }
                else
                {
                    updateClauses.Add($"[{col.ColumnName}] = {paramName}");
                }
            }

            var columns = columnNames.Select(c => $"[{c}]").ToList();

            var sql = $@"
MERGE [dbo].[{definition.TableName}] AS target
USING (SELECT {string.Join(", ", paramNames.Select((p, i) => $"{p} AS [{columnNames[i]}]"))}) AS source
ON {string.Join(" AND ", keyConditions.Select(k => $"target.{k.Split('=')[0].Trim()} = source.{k.Split('=')[0].Trim()}"))}
WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateClauses.Select(u => $"target.{u}"))}
WHEN NOT MATCHED THEN INSERT ({string.Join(", ", columns)}) VALUES ({string.Join(", ", columns.Select(c => $"source.{c}"))});";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters.ToArray());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public override async Task<IReadOnlyList<SchemaDifference>> CompareSchemaAsync(
            string moduleName,
            CancellationToken cancellationToken = default)
        {
            var differences = new List<SchemaDifference>();

            var definitions = GetSchemaDefinitionsForSync()
                .Where(d => d.ModuleName == moduleName)
                .ToList();

            if (!definitions.Any())
            {

                return differences;
            }

            await using var connection = CreateConnection(moduleName);
            await connection.OpenAsync(cancellationToken);

            try
            {
                foreach (var definition in definitions)
                {
                    var tableName = definition.TableName;
                    var tableExists = await TableExistsAsync(connection, "dbo", tableName, cancellationToken);

                    if (!tableExists)
                    {
                        differences.Add(new SchemaDifference
                        {
                            ModelTypeName = definition.ModelType.FullName ?? definition.ModelType.Name,
                            TableName = tableName,
                            DifferenceType = SchemaDifferenceType.TableMissing,
                            Description = $"Table [dbo].[{tableName}] does not exist"
                        });
                        continue;
                    }

                    var dbColumns = await GetTableColumnsAsync(connection, "dbo", tableName, cancellationToken);

                    foreach (var col in definition.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
                    {
                        var dbColumn = dbColumns.FirstOrDefault(c =>
                            c.Name.Equals(col.ColumnName, StringComparison.OrdinalIgnoreCase));

                        if (dbColumn == null)
                        {
                            differences.Add(new SchemaDifference
                            {
                                ModelTypeName = definition.ModelType.FullName ?? definition.ModelType.Name,
                                TableName = tableName,
                                PropertyName = col.PropertyName,
                                ColumnName = col.ColumnName,
                                DifferenceType = SchemaDifferenceType.ColumnMissing,
                                ExpectedType = GetSqlTypeFromColumnDef(col),
                                Description = $"Column [{col.ColumnName}] missing from table"
                            });
                        }
                        else
                        {
                            var expectedType = GetSqlTypeFromColumnDef(col);
                            var expectedLength = col.MaxLength;

                            if (!TypesMatch(expectedType, dbColumn.DataType))
                            {
                                differences.Add(new SchemaDifference
                                {
                                    ModelTypeName = definition.ModelType.FullName ?? definition.ModelType.Name,
                                    TableName = tableName,
                                    PropertyName = col.PropertyName,
                                    ColumnName = col.ColumnName,
                                    DifferenceType = SchemaDifferenceType.ColumnTypeMismatch,
                                    ExpectedType = expectedType,
                                    ActualType = dbColumn.DataType,
                                    Description = $"Type mismatch: expected {expectedType}, found {dbColumn.DataType}"
                                });
                            }
                            else if (expectedLength > 0 && dbColumn.MaxLength > 0 && expectedLength != dbColumn.MaxLength)
                            {
                                differences.Add(new SchemaDifference
                                {
                                    ModelTypeName = definition.ModelType.FullName ?? definition.ModelType.Name,
                                    TableName = tableName,
                                    PropertyName = col.PropertyName,
                                    ColumnName = col.ColumnName,
                                    DifferenceType = SchemaDifferenceType.ColumnLengthMismatch,
                                    ExpectedMaxLength = expectedLength,
                                    ActualMaxLength = dbColumn.MaxLength,
                                    Description = $"Length mismatch: expected {expectedLength}, found {dbColumn.MaxLength}"
                                });
                            }
                        }
                    }
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            return differences;
        }

        public override async Task<SchemaSyncResult> SyncSchemaAsync(
            string moduleName,
            CancellationToken cancellationToken = default)
        {
            var result = new SchemaSyncResult { Success = true };

            var differences = await CompareSchemaAsync(moduleName, cancellationToken);

            if (differences.Count == 0)
            {

                return result;
            }

            await using var connection = CreateConnection(moduleName);
            await connection.OpenAsync(cancellationToken);

            try
            {
                foreach (var diff in differences)
                {
                    try
                    {
                        switch (diff.DifferenceType)
                        {
                            case SchemaDifferenceType.TableMissing:
                                result.Warnings.Add($"Table {diff.TableName} missing - use EnsureCreated or manual creation");
                                break;

                            case SchemaDifferenceType.ColumnMissing:
                                await AddColumnAsync(connection, diff, cancellationToken);
                                result.ColumnsAdded++;
                                result.AppliedChanges.Add(diff);

                                break;

                            case SchemaDifferenceType.ColumnLengthMismatch:
                                if (diff.ExpectedMaxLength > diff.ActualMaxLength)
                                {
                                    await AlterColumnLengthAsync(connection, diff, cancellationToken);
                                    result.ColumnsModified++;
                                    result.AppliedChanges.Add(diff);

                                }
                                else
                                {
                                    result.Warnings.Add($"Cannot shrink column {diff.ColumnName} from {diff.ActualMaxLength} to {diff.ExpectedMaxLength}");
                                }
                                break;

                            case SchemaDifferenceType.ColumnTypeMismatch:
                                result.Warnings.Add($"Type mismatch for {diff.ColumnName} requires manual intervention");
                                break;

                            case SchemaDifferenceType.ExtraColumn:
                                result.Warnings.Add($"Extra column {diff.ColumnName} in {diff.TableName} (not in model)");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to apply {diff.DifferenceType} for {diff.TableName}.{diff.ColumnName}: {ex.Message}");
                        result.Success = false;
                    }
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            return result;
        }

        private async Task<bool> TableExistsAsync(
            System.Data.Common.DbConnection connection,
            string schema,
            string tableName,
            CancellationToken cancellationToken)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";

            var schemaParam = cmd.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            cmd.Parameters.Add(schemaParam);

            var tableParam = cmd.CreateParameter();
            tableParam.ParameterName = "@TableName";
            tableParam.Value = tableName;
            cmd.Parameters.Add(tableParam);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }

        private async Task<List<DbColumnInfo>> GetTableColumnsAsync(
            System.Data.Common.DbConnection connection,
            string schema,
            string tableName,
            CancellationToken cancellationToken)
        {
            var columns = new List<DbColumnInfo>();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";

            var schemaParam = cmd.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            cmd.Parameters.Add(schemaParam);

            var tableParam = cmd.CreateParameter();
            tableParam.ParameterName = "@TableName";
            tableParam.Value = tableName;
            cmd.Parameters.Add(tableParam);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new DbColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.IsDBNull(2) ? -1 : reader.GetInt32(2),
                    IsNullable = reader.GetString(3) == "YES"
                });
            }

            return columns;
        }

        private async Task AddColumnAsync(
            System.Data.Common.DbConnection connection,
            SchemaDifference diff,
            CancellationToken cancellationToken)
        {
            var sqlType = diff.ExpectedType ?? "nvarchar(max)";
            var nullable = "NULL"; // Default to nullable for safety

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"ALTER TABLE [dbo].[{diff.TableName}] ADD [{diff.ColumnName}] {sqlType} {nullable}";

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task AlterColumnLengthAsync(
            System.Data.Common.DbConnection connection,
            SchemaDifference diff,
            CancellationToken cancellationToken)
        {
            var sqlType = $"nvarchar({diff.ExpectedMaxLength})";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"ALTER TABLE [dbo].[{diff.TableName}] ALTER COLUMN [{diff.ColumnName}] {sqlType}";

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static bool TypesMatch(string expected, string actual)
        {
            var e = expected.ToLowerInvariant().Replace(" ", "");
            var a = actual.ToLowerInvariant().Replace(" ", "");

            if (e.StartsWith("nvarchar") && a.StartsWith("nvarchar")) return true;
            if (e.StartsWith("varchar") && a.StartsWith("varchar")) return true;

            if (e == a) return true;

            if (e == "datetime2" && a == "datetime2") return true;
            if (e == "datetimeoffset" && a == "datetimeoffset") return true;

            return false;
        }

        private async Task ProcessPermissionBatchAsync(SqlConnection connection, IReadOnlyList<Core.Data.PermissionRequest> permissionRequests, bool isInvariant, CancellationToken cancellationToken)
        {
            if (permissionRequests.Count == 0)
                return;

            foreach (var request in permissionRequests)
            {
                await ProcessPermissionRequestAsync(connection, request, isInvariant, cancellationToken);
            }
        }

        private async Task ProcessPermissionRequestAsync(SqlConnection connection, Core.Data.PermissionRequest request, bool isInvariant, CancellationToken cancellationToken)
        {
            // Resolve RoleName to RoleId
            var roleId = await GetRoleIdByNameAsync(connection, request.RoleName, cancellationToken);
            if (!roleId.HasValue)
            {
                Console.WriteLine($"Warning: Role '{request.RoleName}' not found, skipping permission for {request.ModuleName}.{request.ModelName}");
                return;
            }

            // Resolve ModuleName + ModelName to ModelRecord Id
            var modelRecordId = await GetModelRecordIdByNameAsync(connection, request.ModuleName, request.ModelName, cancellationToken);
            if (!modelRecordId.HasValue)
            {
                Console.WriteLine($"Warning: ModelRecord for '{request.ModuleName}.{request.ModelName}' not found, skipping permission");
                return;
            }

            // Check if permission exists
            var exists = await PermissionExistsAsync(connection, roleId.Value, modelRecordId.Value, cancellationToken);

            if (isInvariant)
            {
                // Invariant: Upsert every time
                if (exists)
                {
                    await UpdatePermissionAsync(connection, roleId.Value, modelRecordId.Value, request, cancellationToken);
                }
                else
                {
                    await CreatePermissionAsync(connection, roleId.Value, modelRecordId.Value, request, isInvariant, cancellationToken);
                }
            }
            else
            {
                // Seed: Insert once only if doesn't exist
                if (!exists)
                {
                    await CreatePermissionAsync(connection, roleId.Value, modelRecordId.Value, request, isInvariant, cancellationToken);
                }
            }
        }

        private async Task<Guid?> GetRoleIdByNameAsync(SqlConnection connection, string roleName, CancellationToken cancellationToken)
        {
            const string query = "SELECT Id FROM Role WHERE Name = @RoleName";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@RoleName", roleName);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result as Guid?;
        }

        private async Task<Guid?> GetModelRecordIdByNameAsync(SqlConnection connection, string moduleName, string modelName, CancellationToken cancellationToken)
        {
            const string query = @"
                SELECT m.Id 
                FROM ModelRecord m
                INNER JOIN RADModule mod ON m.ModuleId = mod.Id
                WHERE mod.Name = @ModuleName AND m.Name = @ModelName";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@ModuleName", moduleName);
            cmd.Parameters.AddWithValue("@ModelName", modelName);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result as Guid?;
        }

        private async Task<bool> PermissionExistsAsync(SqlConnection connection, Guid roleId, Guid recordId, CancellationToken cancellationToken)
        {
            const string query = "SELECT COUNT(1) FROM Permission WHERE RoleId = @RoleId AND RecordId = @RecordId";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@RecordId", recordId);

            var result = (int)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
            return result > 0;
        }

        private async Task CreatePermissionAsync(SqlConnection connection, Guid roleId, Guid recordId, Core.Data.PermissionRequest request, bool isInvariant, CancellationToken cancellationToken)
        {
            const string query = @"
                INSERT INTO Permission (RoleId, RecordId, UsePages, CanCreate, ReadLevel, UpdateLevel, DeleteLevel, 
                                       IsInvariant, OwnershipLevel, CreatedByDisplayName, CreatedOn)
                VALUES (@RoleId, @RecordId, @UsePages, @CanCreate, @ReadLevel, @UpdateLevel, @DeleteLevel, 
                        @IsInvariant, @OwnershipLevel, 'System', GETUTCDATE())";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@RecordId", recordId);
            cmd.Parameters.AddWithValue("@UsePages", request.UsePages);
            cmd.Parameters.AddWithValue("@CanCreate", request.CanCreate);
            cmd.Parameters.AddWithValue("@ReadLevel", (int)request.ReadLevel);
            cmd.Parameters.AddWithValue("@UpdateLevel", (int)request.UpdateLevel);
            cmd.Parameters.AddWithValue("@DeleteLevel", (int)request.DeleteLevel);
            cmd.Parameters.AddWithValue("@IsInvariant", isInvariant);
            cmd.Parameters.AddWithValue("@OwnershipLevel", 2); // Public

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task UpdatePermissionAsync(SqlConnection connection, Guid roleId, Guid recordId, Core.Data.PermissionRequest request, CancellationToken cancellationToken)
        {
            const string query = @"
                UPDATE Permission 
                SET UsePages = @UsePages, 
                    CanCreate = @CanCreate, 
                    ReadLevel = @ReadLevel, 
                    UpdateLevel = @UpdateLevel, 
                    DeleteLevel = @DeleteLevel,
                    ModifiedByDisplayName = 'System',
                    ModifiedOn = GETUTCDATE()
                WHERE RoleId = @RoleId AND RecordId = @RecordId";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@RecordId", recordId);
            cmd.Parameters.AddWithValue("@UsePages", request.UsePages);
            cmd.Parameters.AddWithValue("@CanCreate", request.CanCreate);
            cmd.Parameters.AddWithValue("@ReadLevel", (int)request.ReadLevel);
            cmd.Parameters.AddWithValue("@UpdateLevel", (int)request.UpdateLevel);
            cmd.Parameters.AddWithValue("@DeleteLevel", (int)request.DeleteLevel);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private class DbColumnInfo
        {
            public string Name { get; set; } = string.Empty;
            public string DataType { get; set; } = string.Empty;
            public int MaxLength { get; set; }
            public bool IsNullable { get; set; }
        }
    }
}
