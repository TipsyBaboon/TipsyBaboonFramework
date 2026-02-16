using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.Core.Extensions;
using TipsyBaboon.Core.FrameworkBase;
using System.Xml.Schema;
using System.Text;

namespace TipsyBaboon.SqlServer.Storage
{
    /// <summary>
    /// SQL Server implementation of <see cref="BaseModelStore"/>.
    /// Provides all CRUD, query, paging, and bulk operations against SQL Server using raw ADO.NET (no Entity Framework).
    /// Handles lifecycle hooks (<see cref="IModelWithSaveAction"/>, <see cref="IModelWithPostSaveAction"/>,
    /// <see cref="IModelWithGetAction"/>), invariant record protection, change logging, and navigation property eager loading.
    /// </summary>
    public class TipsyBaboonModelStore : BaseModelStore
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly Lazy<IViewModelStore?> _viewModelStore;

        private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TipsyBaboonModelStore(
            IServiceProvider serviceProvider,
            IHttpContextAccessor? httpContextAccessor = null,
            IViewModelStore? viewModelStore = null)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
            _viewModelStore = new Lazy<IViewModelStore?>(() => viewModelStore);
        }

        #region Query

        public override async Task<PagedResult<T>> QueryTypedAsync<T>(PagedRequest request, CancellationToken cancellationToken = default)
        {
            var result = await QueryCoreAsync(typeof(T), request, cancellationToken);
            return PagedResult<T>.Create(result.Items.Cast<T>().ToList(), result.TotalCount, result.Page, result.PageSize);
        }

        #endregion

        #region Untyped Core Methods (SQL Execution - Abstract Overrides)

        protected override async Task<object?> GetByIdCoreAsync(Type entityType, object id, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            
            var keyColumns = schema.KeyColumns;
            Dictionary<string, object?> keyValues;

            if (id is IDictionary<string, object?> keyDict)
            {
                keyValues = new Dictionary<string, object?>(keyDict);
            }
            else if (keyColumns.Count == 1)
            {
                var keyCol = keyColumns[0];
                var convertedValue = ConvertKeyValue(id, keyCol.ClrType);
                keyValues = new Dictionary<string, object?> { { keyCol.PropertyName, convertedValue } };
            }
            else
            {
                throw new ArgumentException(
                    $"Composite key requires a dictionary with keys: {string.Join(", ", keyColumns.Select(k => k.PropertyName))}",
                    nameof(id));
            }

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var sourceName = GetSourceName(schema);
            var columnList = string.Join(", ", schema.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped).Select(c => $"[{c.ColumnName}]"));
            var whereClause = BuildKeyWhereClause(schema, out _);
            var sql = $"SELECT {columnList} FROM [dbo].[{sourceName}]{whereClause}";

            await using var cmd = new SqlCommand(sql, conn);
            AddKeyParameters(cmd, schema, keyValues);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var entity = MapEntity(reader, entityType, schema);
                if (entity != null)
                {
                    var parentId = GetPrimaryKeyValue(entity, schema);
                    if (parentId != null && entity is TipsyBaboonModel tbEntity)
                        await LoadNavigationPropertiesForEntityAsync(tbEntity, schema, inDetail: true, cancellationToken);
                }
                return entity;
            }
            return null;
        }

        protected override async Task<object> InsertCoreAsync(Type entityType, object entity, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);

            var typedEntity = ApplyDynamicToEntity(entity, entityType, schema);
            
            if (typedEntity is TipsyBaboonModel tbEntity)
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId.HasValue)
                {
                    tbEntity.OwnerId = currentUserId.Value;
                }

                tbEntity.CreatedOn = DateTime.UtcNow;
                tbEntity.CreatedById = currentUserId;
                tbEntity.CreatedByDisplayName = await GetCurrentUserDisplayNameAsync(cancellationToken);
            }
            
            var keyValues = ExtractKeyValuesFromObject(typedEntity, schema);
            var keyDisplay = GetKeyDisplayString(keyValues);

            if (!skipHooks && typedEntity is IModelWithSaveAction actionable)
            {
                var committingUser = GetCurrentUser();
                var response = await actionable.BeforeChangeAsync(ActionType.Create, _serviceProvider, committingUser, cancellationToken);
                
                if (response == SaveResponseType.ChangeApplied)
                {
                    return typedEntity;
                }
                else if (response == SaveResponseType.Error)
                {
                    throw new InvalidOperationException($"BeforeChangeAsync reported an error for {entityType.Name} with key {keyDisplay}");
                }
            }

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var columns = schema.Columns
                .Where(c => !c.IsNavigationProperty && !c.IsNotMapped && 
                    c.DatabaseGenerated != System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)
                .ToList();
            
            var identityColumns = schema.Columns
                .Where(c => !c.IsNavigationProperty && !c.IsNotMapped && 
                    c.DatabaseGenerated == System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)
                .ToList();
            
            var columnNames = string.Join(", ", columns.Select(c => $"[{c.ColumnName}]"));
            var paramNames = string.Join(", ", columns.Select(c => $"@{c.PropertyName}"));

            string sql;
            if (identityColumns.Any())
            {
                var outputColumns = string.Join(", ", identityColumns.Select(c => $"INSERTED.[{c.ColumnName}]"));
                sql = $"INSERT INTO [dbo].[{sourceName}] ({columnNames}) OUTPUT {outputColumns} VALUES ({paramNames})";
            }
            else
            {
                sql = $"INSERT INTO [dbo].[{sourceName}] ({columnNames}) VALUES ({paramNames})";
            }

            await using var cmd = new SqlCommand(sql, conn);
            AddParameters(cmd, typedEntity, entityType, columns);

            try
            {
                if (identityColumns.Any())
                {
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        for (int i = 0; i < identityColumns.Count; i++)
                        {
                            var identityCol = identityColumns[i];
                            var generatedValue = reader.GetValue(i);
                            if (generatedValue != DBNull.Value)
                            {
                                var prop = entityType.GetProperty(identityCol.PropertyName);
                                if (prop != null && prop.CanWrite)
                                {
                                    var convertedValue = Convert.ChangeType(generatedValue, prop.PropertyType);
                                    prop.SetValue(typedEntity, convertedValue);
                                }
                            }
                        }
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                if (!skipHooks && typedEntity is IModelWithPostSaveAction postSaveActionable)
                {
                    var committingUser = GetCurrentUser();
                    await postSaveActionable.AfterSaveAsync(ActionType.Create, committingUser, cancellationToken);
                }

                if (!skipChangeLog && typedEntity is TipsyBaboonModel tbm)
                {
                    await WriteChangeLogCoreAsync(tbm, entityType, schema, ActionType.Create, null, cancellationToken);
                }

                return typedEntity;
            }
            catch (SqlException)
            {
                throw;
            }
        }

        protected override async Task<object> UpdateCoreAsync(Type entityType, object entity, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);
            var keyColumns = schema.KeyColumns;

            var keyValues = ExtractKeyValuesFromDynamic(entity, schema);
            var keyDisplay = GetKeyDisplayString(keyValues);
            if (keyValues.Count == 0 || keyValues.Values.All(v => v == null))
                throw new ArgumentException($"Entity must have key properties ({string.Join(", ", keyColumns.Select(k => k.PropertyName))}) for update", nameof(entity));

            var existing = await GetByIdCoreAsync(entityType, keyValues, cancellationToken) as TipsyBaboonModel;
            if (existing == null)
                throw new InvalidOperationException($"Cannot update {entityType.Name}: entity with key {keyDisplay} not found");

            if (!ignoreInvariant && existing.IsInvariant)
                throw new InvalidOperationException($"Cannot update {entityType.Name} with key {keyDisplay}: record is marked as invariant and cannot be modified.");

            var typedEntity = ApplyDynamicToEntity(entity, existing, entityType, schema);
            
            if (typedEntity is TipsyBaboonModel tbEntity)
            {
                tbEntity.OwnerId = existing.OwnerId;

                if (!allowChangeInvariant)
                {
                    tbEntity.IsInvariant = existing.IsInvariant;
                }

                tbEntity.ModifiedOn = DateTime.UtcNow;
                tbEntity.ModifiedById = GetCurrentUserId();
                tbEntity.ModifiedByDisplayName = await GetCurrentUserDisplayNameAsync(cancellationToken);
            }
            
            var entityKeyValues = ExtractKeyValuesFromObject(typedEntity, schema);
            var entityKeyDisplay = GetKeyDisplayString(entityKeyValues);

            if (!skipHooks && typedEntity is IModelWithSaveAction actionable)
            {
                actionable.OriginalState = existing.CloneObject();
                var committingUser = GetCurrentUser();
                var response = await actionable.BeforeChangeAsync(ActionType.Update, _serviceProvider, committingUser, cancellationToken);
                
                if (response == SaveResponseType.ChangeApplied)
                {
                    return typedEntity;
                }
                else if (response == SaveResponseType.Error)
                {
                    throw new InvalidOperationException($"BeforeChangeAsync reported an error for {entityType.Name} with key {entityKeyDisplay}");
                }
            }

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var keyPropertyNames = new HashSet<string>(keyColumns.Select(k => k.PropertyName));
            var updateColumns = schema.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped && !keyPropertyNames.Contains(c.PropertyName)).ToList();
            var setClause = string.Join(", ", updateColumns.Select(c => $"[{c.ColumnName}] = @{c.PropertyName}"));
            var whereClause = BuildKeyWhereClause(schema, out _);

            var sql = $"UPDATE [dbo].[{sourceName}] SET {setClause}{whereClause}";

            await using var cmd = new SqlCommand(sql, conn);
            AddParameters(cmd, typedEntity, entityType, updateColumns);
            AddKeyParameters(cmd, schema, entityKeyValues);

            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                if (!skipHooks && typedEntity is IModelWithPostSaveAction postSaveActionable)
                {
                    var committingUser = GetCurrentUser();
                    await postSaveActionable.AfterSaveAsync(ActionType.Update, committingUser, cancellationToken);
                }

                if (!skipChangeLog && typedEntity is TipsyBaboonModel tbm)
                {
                    await WriteChangeLogCoreAsync(tbm, entityType, schema, ActionType.Update, null, cancellationToken);
                }

                return typedEntity;
            }
            catch (SqlException)
            {
                throw;
            }
        }

        protected override async Task<object> UpsertCoreAsync(Type entityType, object entity, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var keyColumns = schema.KeyColumns;

            var keyValues = ExtractKeyValuesFromDynamic(entity, schema);
            
            if (keyValues.Count == 0 || keyValues.Values.All(v => v == null))
            {
                return await InsertCoreAsync(entityType, entity, skipHooks: skipHooks, skipChangeLog: skipChangeLog, cancellationToken: cancellationToken);
            }

            var existing = await GetByIdCoreAsync(entityType, keyValues, cancellationToken);
            
            if (existing != null)
            {
                return await UpdateCoreAsync(entityType, entity, skipHooks: skipHooks, ignoreInvariant: ignoreInvariant, allowChangeInvariant: allowChangeInvariant, skipChangeLog: skipChangeLog, cancellationToken: cancellationToken);
            }
            else
            {
                return await InsertCoreAsync(entityType, entity, skipHooks: skipHooks, skipChangeLog: skipChangeLog, cancellationToken: cancellationToken);
            }
        }

        protected override async Task<bool> DeleteCoreAsync(Type entityType, object id, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var keyColumns = schema.KeyColumns;
            Dictionary<string, object?> keyValues;

            if (id is IDictionary<string, object?> keyDict)
            {
                keyValues = new Dictionary<string, object?>(keyDict);
            }
            else if (keyColumns.Count == 1)
            {
                var keyCol = keyColumns[0];
                var convertedValue = ConvertKeyValue(id, keyCol.ClrType);
                keyValues = new Dictionary<string, object?> { { keyCol.PropertyName, convertedValue } };
            }
            else
            {
                throw new ArgumentException(
                    $"Composite key requires a dictionary with keys: {string.Join(", ", keyColumns.Select(k => k.PropertyName))}",
                    nameof(id));
            }

            var entity = await GetByIdCoreAsync(entityType, keyValues, cancellationToken) as TipsyBaboonModel
                ?? throw new InvalidOperationException(
                    $"Cannot delete {entityType.Name}: entity with key {GetKeyDisplayString(keyValues)} not found");

            if (!ignoreInvariant && entity.IsInvariant)
                throw new InvalidOperationException($"Cannot delete {entityType.Name} with key {GetKeyDisplayString(keyValues)}: record is marked as invariant and cannot be deleted.");

            var entityKeyValues = ExtractKeyValuesFromObject(entity, schema);

            if (!skipHooks && entity is IModelWithSaveAction actionable)
            {
                var committingUser = GetCurrentUser();
                var response = await actionable.BeforeChangeAsync(ActionType.Delete, _serviceProvider, committingUser, cancellationToken);

                if (response == SaveResponseType.ChangeApplied)
                {
                    return true;
                }
                else if (response == SaveResponseType.Error)
                {
                    throw new InvalidOperationException($"BeforeChangeAsync reported an error for {entityType.Name} with key {GetKeyDisplayString(entityKeyValues)}");
                }
            }

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var sourceName = GetSourceName(schema);
            var whereClause = BuildKeyWhereClause(schema, out _);
            var sql = $"DELETE FROM [dbo].[{sourceName}]{whereClause}";
            await using var cmd = new SqlCommand(sql, conn);
            AddKeyParameters(cmd, schema, entityKeyValues);

            var result = await cmd.ExecuteNonQueryAsync(cancellationToken);

            if (result > 0)
            {
                if (!skipHooks && entity is IModelWithPostSaveAction postSaveActionable)
                {
                    var committingUser = GetCurrentUser();
                    await postSaveActionable.AfterSaveAsync(ActionType.Delete, committingUser, cancellationToken);
                }

                if (!skipChangeLog)
                {
                    await WriteChangeLogCoreAsync(entity, entityType, schema, ActionType.Delete, null, cancellationToken);
                }
            }

            return result > 0;
        }

        protected override async Task<PagedResult<object>> QueryCoreAsync(Type entityType, PagedRequest request, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);
            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            // Filter permission views and governance tables to only show registered modules/models
            if (entityType.Name == "RolePermission" || entityType.Name == "ModelPermission")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                if (registeredModules.Any())
                {
                    var moduleFilter = FilterCriteria.In("ModuleName", registeredModules.ToList());
                    if (!request.Filters.Any(f => f.PropertyName.Equals("ModuleName", StringComparison.OrdinalIgnoreCase)))
                    {
                        request.Filters.Add(moduleFilter);
                    }
                }
            }
            else if (entityType.Name == "RADModule")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                if (registeredModules.Any())
                {
                    var moduleFilter = FilterCriteria.In("Name", registeredModules.ToList());
                    if (!request.Filters.Any(f => f.PropertyName.Equals("Name", StringComparison.OrdinalIgnoreCase)))
                    {
                        request.Filters.Add(moduleFilter);
                    }
                }
            }
            else if (entityType.Name == "ModelRecord" || entityType.Name == "Privilege")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                if (registeredModules.Any())
                {
                    // Get registered ModelRecord IDs from ModelRegistry
                    var registeredModelIds = new List<Guid>();
                    foreach (var moduleName in registeredModules)
                    {
                        var moduleModels = GetAllSchemaDefinitions()
                            .Where(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                        registeredModelIds.AddRange(moduleModels.Select(m => m.RecordModelId));
                    }
                    
                    if (registeredModelIds.Any())
                    {
                        var filterProperty = entityType.Name == "Privilege" ? "RecordId" : "Id";
                        var modelFilter = FilterCriteria.In(filterProperty, registeredModelIds);
                        if (!request.Filters.Any(f => f.PropertyName.Equals(filterProperty, StringComparison.OrdinalIgnoreCase)))
                        {
                            request.Filters.Add(modelFilter);
                        }
                    }
                }
            }
            else if (entityType.Name == "RolePrivilegeView")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                if (registeredModules.Any())
                {
                    // Get registered ModelRecord IDs from ModelRegistry
                    var registeredModelIds = new List<Guid>();
                    foreach (var moduleName in registeredModules)
                    {
                        var moduleModels = BaseModelStore.GetAllSchemaDefinitions()
                            .Where(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                        registeredModelIds.AddRange(moduleModels.Select(m => m.RecordModelId));
                    }
                    
                    if (registeredModelIds.Any())
                    {
                        var modelFilter = FilterCriteria.In("ModelId", registeredModelIds);
                        if (!request.Filters.Any(f => f.PropertyName.Equals("ModelId", StringComparison.OrdinalIgnoreCase)))
                        {
                            request.Filters.Add(modelFilter);
                        }
                    }
                }
            }

            var whereClause = BuildWhereClause(schema, request.Filters, out var parameters);
            var orderByClause = BuildOrderByClause(schema, request.Sorts);

            var countSql = $"SELECT COUNT(*) FROM [dbo].[{sourceName}]{whereClause}";
            await using var countCmd = new SqlCommand(countSql, conn);
            AddFilterParameters(countCmd, parameters);
            var totalCount = (int)(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);

            if (totalCount == 0)
            {
                return new PagedResult<object>
                {
                    Items = new List<object>(),
                    TotalCount = 0,
                    Page = request.Page,
                    PageSize = request.PageSize
                };
            }

            var columnList = string.Join(", ", schema.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped).Select(c => $"[{c.ColumnName}]"));
            
            string dataSql;
            if (request.IsPaged)
            {
                dataSql = $@"
                    SELECT {columnList}
                    FROM [dbo].[{sourceName}]
                    {whereClause}
                    {orderByClause}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            }
            else
            {
                dataSql = $@"
                    SELECT {columnList}
                    FROM [dbo].[{sourceName}]
                    {whereClause}
                    {orderByClause}";
            }

            await using var dataCmd = new SqlCommand(dataSql, conn);
            AddFilterParameters(dataCmd, parameters);
            if (request.IsPaged)
            {
                dataCmd.Parameters.AddWithValue("@Offset", request.Offset);
                dataCmd.Parameters.AddWithValue("@PageSize", request.PageSize!.Value);
            }

            var items = new List<object>();
            await using var reader = await dataCmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapEntity(reader, entityType, schema));
            }

            await reader.CloseAsync();

            if (items.Any() && items[0] is TipsyBaboonModel)
            {
                var tbItems = items.Cast<TipsyBaboonModel>().ToList();
                await LoadNavigationPropertiesForCollectionUntypedAsync(tbItems, schema, inDetail: false, cancellationToken);
            }

            return new PagedResult<object>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        protected override async Task<IEnumerable<object>> GetByIdsCoreAsync(Type entityType, IEnumerable<object> ids, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            
            if (schema.KeyColumns.Count > 1)
            {
                throw new NotSupportedException($"GetByIdsCoreAsync is not supported for composite key entities. Use QueryCoreAsync with filters instead.");
            }
            
            var keyColumn = schema.KeyColumns.FirstOrDefault();
            if (keyColumn == null)
            {
                throw new InvalidOperationException($"No key column defined for entity {entityType.Name}");
            }
            
            var keyValues = ids.Select(id => ConvertKeyValue(id, keyColumn.ClrType)).ToList();

            if (!keyValues.Any())
                return Enumerable.Empty<object>();

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var sourceName = GetSourceName(schema);
            var columnList = string.Join(", ", schema.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped).Select(c => $"[{c.ColumnName}]"));
            var idParams = string.Join(", ", keyValues.Select((_, i) => $"@Key{i}"));
            var sql = $"SELECT {columnList} FROM [dbo].[{sourceName}] WHERE [{keyColumn.ColumnName}] IN ({idParams})";

            await using var cmd = new SqlCommand(sql, conn);
            for (int i = 0; i < keyValues.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@Key{i}", keyValues[i] ?? DBNull.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var results = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapEntity(reader, entityType, schema));
            }
            
            if (results.Any() && results[0] is TipsyBaboonModel)
            {
                var tbItems = results.Cast<TipsyBaboonModel>().ToList();
                await LoadNavigationPropertiesForCollectionUntypedAsync(tbItems, schema, inDetail: false, cancellationToken);
            }
            
            return results;
        }

        protected override async Task<IEnumerable<object>> BulkInsertCoreAsync(Type entityType, IEnumerable<object> entities, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);
            var entitiesList = entities.ToList();

            if (!entitiesList.Any())
                return Enumerable.Empty<object>();

            var typedEntities = new List<object>();
            var currentUserId = GetCurrentUserId();
            var currentUserDisplayName = await GetCurrentUserDisplayNameAsync(cancellationToken);
            var currentUser = GetCurrentUser();
            var now = DateTime.UtcNow;

            foreach (var entity in entitiesList)
            {
                var typedEntity = ApplyDynamicToEntity(entity, entityType, schema);

                if (typedEntity is TipsyBaboonModel tbEntity)
                {
                    if (currentUserId.HasValue)
                    {
                        tbEntity.OwnerId = currentUserId.Value;
                    }

                    tbEntity.CreatedOn = now;
                    tbEntity.CreatedById = currentUserId;
                    tbEntity.CreatedByDisplayName = currentUserDisplayName;
                }

                typedEntities.Add(typedEntity);
            }

            if (!skipHooks)
            {
                foreach (var typedEntity in typedEntities)
                {
                    if (typedEntity is IModelWithSaveAction actionable)
                    {
                        var response = await actionable.BeforeChangeAsync(ActionType.Create, _serviceProvider, currentUser, cancellationToken);

                        if (response == SaveResponseType.Error)
                        {
                            var keyValues = ExtractKeyValuesFromObject(typedEntity, schema);
                            var keyDisplay = GetKeyDisplayString(keyValues);
                            throw new InvalidOperationException($"BeforeChangeAsync reported an error for {entityType.Name} with key {keyDisplay}");
                        }
                    }
                }
            }

            var columns = schema.Columns
                .Where(c => !c.IsNavigationProperty && !c.IsNotMapped &&
                    c.DatabaseGenerated != System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)
                .ToList();

            var dataTable = new DataTable();
            foreach (var col in columns)
            {
                var columnType = Nullable.GetUnderlyingType(col.ClrType) ?? col.ClrType;
                if (columnType.IsEnum)
                    columnType = typeof(int);
                else if (columnType == typeof(DateOnly))
                    columnType = typeof(DateTime);
                else if (columnType == typeof(TimeOnly))
                    columnType = typeof(TimeSpan);

                dataTable.Columns.Add(col.ColumnName, columnType);
            }

            foreach (var typedEntity in typedEntities)
            {
                var row = dataTable.NewRow();
                foreach (var col in columns)
                {
                    var prop = entityType.GetProperty(col.PropertyName);
                    var value = prop?.GetValue(typedEntity);

                    if (value == null)
                    {
                        row[col.ColumnName] = DBNull.Value;
                    }
                    else if (value is DateOnly dateOnly)
                    {
                        row[col.ColumnName] = dateOnly.ToDateTime(TimeOnly.MinValue);
                    }
                    else if (value is TimeOnly timeOnly)
                    {
                        row[col.ColumnName] = timeOnly.ToTimeSpan();
                    }
                    else if (value.GetType().IsEnum)
                    {
                        row[col.ColumnName] = (int)value;
                    }
                    else
                    {
                        row[col.ColumnName] = value;
                    }
                }
                dataTable.Rows.Add(row);
            }

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            using var bulkCopy = new SqlBulkCopy(conn);
            bulkCopy.DestinationTableName = $"[dbo].[{sourceName}]";
            bulkCopy.BatchSize = 1000;
            bulkCopy.BulkCopyTimeout = 300;

            foreach (var col in columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);

            if (!skipHooks)
            {
                foreach (var typedEntity in typedEntities)
                {
                    if (typedEntity is IModelWithPostSaveAction postSaveActionable)
                    {
                        await postSaveActionable.AfterSaveAsync(ActionType.Create, currentUser, cancellationToken);
                    }
                }
            }

            if (!skipChangeLog)
            {
                var changeLog = new ChangeLog
                {
                    ModuleName = schema.ModuleName,
                    ModelTypeName = entityType.Name,
                    RecordId = null,
                    RecordName = $"Bulk Insert of {typedEntities.Count} records",
                    ActionType = ActionType.Create,
                    ChangedById = currentUserId,
                    ChangedByDisplayName = currentUserDisplayName,
                    CreatedOn = DateTime.UtcNow,
                    CreatedById = currentUserId,
                    CreatedByDisplayName = currentUserDisplayName,
                    DescriptionOfChange = $"Bulk insert of {typedEntities.Count} {entityType.Name} records"
                };

                await InsertCoreAsync(typeof(ChangeLog), changeLog, skipHooks: true, skipChangeLog: true, cancellationToken: cancellationToken);
            }

            return typedEntities;
        }

        protected override async Task<IEnumerable<object>> BulkUpdateCoreAsync(Type entityType, IEnumerable<object> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);
            var entitiesList = entities.ToList();

            if (!entitiesList.Any())
                return Enumerable.Empty<object>();

            var keyColumns = schema.KeyColumns;
            if (keyColumns.Count == 0)
                throw new InvalidOperationException($"Cannot bulk update {entityType.Name}: no key columns defined");

            var currentUserId = GetCurrentUserId();
            var currentUserDisplayName = await GetCurrentUserDisplayNameAsync(cancellationToken);
            var currentUser = GetCurrentUser();
            var now = DateTime.UtcNow;

            var entityKeyValuePairs = new List<(object entityObj, Dictionary<string, object?> keyValues)>();
            foreach (var entity in entitiesList)
            {
                var keyValues = ExtractKeyValuesFromDynamic(entity, schema);
                if (keyValues.Count == 0 || keyValues.Values.All(v => v == null))
                    throw new ArgumentException($"Entity must have key properties ({string.Join(", ", keyColumns.Select(k => k.PropertyName))}) for bulk update");

                entityKeyValuePairs.Add((entity, keyValues));
            }

            var existingRecordsDict = new Dictionary<string, TipsyBaboonModel>();
            foreach (var (entityObj, keyValues) in entityKeyValuePairs)
            {
                var existing = await GetByIdCoreAsync(entityType, keyValues, cancellationToken) as TipsyBaboonModel;
                var keyDisplay = GetKeyDisplayString(keyValues);

                if (existing == null)
                    throw new InvalidOperationException($"Cannot update {entityType.Name}: entity with key {keyDisplay} not found");

                if (!ignoreInvariant && existing.IsInvariant)
                    throw new InvalidOperationException($"Cannot update {entityType.Name} with key {keyDisplay}: record is marked as invariant and cannot be modified.");

                existingRecordsDict[keyDisplay] = existing;
            }

            var typedEntities = new List<object>();
            foreach (var (entityObj, keyValues) in entityKeyValuePairs)
            {
                var keyDisplay = GetKeyDisplayString(keyValues);
                var existing = existingRecordsDict[keyDisplay];

                var typedEntity = ApplyDynamicToEntity(entityObj, existing, entityType, schema);

                if (typedEntity is TipsyBaboonModel tbEntity)
                {
                    tbEntity.OwnerId = existing.OwnerId;
                    
                    if (!allowChangeInvariant)
                    {
                        tbEntity.IsInvariant = existing.IsInvariant;
                    }
                    
                    tbEntity.ModifiedOn = now;
                    tbEntity.ModifiedById = currentUserId;
                    tbEntity.ModifiedByDisplayName = currentUserDisplayName;
                }

                typedEntities.Add(typedEntity);
            }

            if (!skipHooks)
            {
                foreach (var typedEntity in typedEntities)
                {
                    if (typedEntity is IModelWithSaveAction actionable)
                    {
                        var entityKeyValues = ExtractKeyValuesFromObject(typedEntity, schema);
                        var existing = existingRecordsDict[GetKeyDisplayString(entityKeyValues)];
                        actionable.OriginalState = existing.CloneObject();

                        var response = await actionable.BeforeChangeAsync(ActionType.Update, _serviceProvider, currentUser, cancellationToken);

                        if (response == SaveResponseType.Error)
                        {
                            var keyDisplay = GetKeyDisplayString(entityKeyValues);
                            throw new InvalidOperationException($"BeforeChangeAsync reported an error for {entityType.Name} with key {keyDisplay}");
                        }
                    }
                }
            }

            var keyPropertyNames = new HashSet<string>(keyColumns.Select(k => k.PropertyName));
            var updateColumns = schema.Columns
                .Where(c => !c.IsNavigationProperty && !c.IsNotMapped)
                .ToList();

            var tempTableName = $"#TempUpdate_{Guid.NewGuid():N}";

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var createTempTableSql = new StringBuilder();
            createTempTableSql.AppendLine($"CREATE TABLE {tempTableName} (");

            var tempTableColumns = new List<string>();
            foreach (var col in updateColumns)
            {
                var columnType = GetSqlServerType(col);
                tempTableColumns.Add($"[{col.ColumnName}] {columnType}");
            }
            createTempTableSql.AppendLine(string.Join(",\n", tempTableColumns));
            createTempTableSql.AppendLine(")");

            await using (var createCmd = new SqlCommand(createTempTableSql.ToString(), conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var dataTable = new DataTable();
            foreach (var col in updateColumns)
            {
                var columnType = Nullable.GetUnderlyingType(col.ClrType) ?? col.ClrType;
                if (columnType.IsEnum)
                    columnType = typeof(int);
                else if (columnType == typeof(DateOnly))
                    columnType = typeof(DateTime);
                else if (columnType == typeof(TimeOnly))
                    columnType = typeof(TimeSpan);

                dataTable.Columns.Add(col.ColumnName, columnType);
            }

            foreach (var typedEntity in typedEntities)
            {
                var row = dataTable.NewRow();
                foreach (var col in updateColumns)
                {
                    var prop = entityType.GetProperty(col.PropertyName);
                    var value = prop?.GetValue(typedEntity);

                    if (value == null)
                    {
                        row[col.ColumnName] = DBNull.Value;
                    }
                    else if (value is DateOnly dateOnly)
                    {
                        row[col.ColumnName] = dateOnly.ToDateTime(TimeOnly.MinValue);
                    }
                    else if (value is TimeOnly timeOnly)
                    {
                        row[col.ColumnName] = timeOnly.ToTimeSpan();
                    }
                    else if (value.GetType().IsEnum)
                    {
                        row[col.ColumnName] = (int)value;
                    }
                    else
                    {
                        row[col.ColumnName] = value;
                    }
                }
                dataTable.Rows.Add(row);
            }

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = tempTableName;
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300;

                foreach (var col in updateColumns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            }

            var updateSql = new StringBuilder();
            updateSql.AppendLine($"UPDATE target SET");

            var setStatements = updateColumns
                .Where(c => !keyPropertyNames.Contains(c.PropertyName))
                .Select(c => $"target.[{c.ColumnName}] = temp.[{c.ColumnName}]");
            updateSql.AppendLine(string.Join(",\n", setStatements));

            updateSql.AppendLine($"FROM [dbo].[{sourceName}] AS target");
            updateSql.AppendLine($"INNER JOIN {tempTableName} AS temp ON");

            var joinConditions = keyColumns.Select(k => $"target.[{k.ColumnName}] = temp.[{k.ColumnName}]");
            updateSql.AppendLine(string.Join(" AND ", joinConditions));

            await using (var updateCmd = new SqlCommand(updateSql.ToString(), conn))
            {
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var dropCmd = new SqlCommand($"DROP TABLE {tempTableName}", conn))
            {
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!skipHooks)
            {
                foreach (var typedEntity in typedEntities)
                {
                    if (typedEntity is IModelWithPostSaveAction postSaveActionable)
                    {
                        await postSaveActionable.AfterSaveAsync(ActionType.Update, currentUser, cancellationToken);
                    }
                }
            }

            if (!skipChangeLog)
            {
                var changeLog = new ChangeLog
                {
                    ModuleName = schema.ModuleName,
                    ModelTypeName = entityType.Name,
                    RecordId = null,
                    RecordName = $"Bulk Update of {typedEntities.Count} records",
                    ActionType = ActionType.Update,
                    ChangedById = currentUserId,
                    ChangedByDisplayName = currentUserDisplayName,
                    CreatedOn = DateTime.UtcNow,
                    CreatedById = currentUserId,
                    CreatedByDisplayName = currentUserDisplayName,
                    DescriptionOfChange = $"Bulk update of {typedEntities.Count} {entityType.Name} records"
                };

                await InsertCoreAsync(typeof(ChangeLog), changeLog, skipHooks: true, skipChangeLog: true, cancellationToken: cancellationToken);
            }

            return typedEntities;
        }

        protected override async Task<IEnumerable<object>> BulkUpsertCoreAsync(Type entityType, IEnumerable<object> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var entitiesList = entities.ToList();

            if (!entitiesList.Any())
                return Enumerable.Empty<object>();

            var keyColumns = schema.KeyColumns;
            if (keyColumns.Count == 0)
                throw new InvalidOperationException($"Cannot bulk upsert {entityType.Name}: no key columns defined");

            var entityKeyValuePairs = new List<(object entityObj, Dictionary<string, object?> keyValues)>();
            foreach (var entity in entitiesList)
            {
                var keyValues = ExtractKeyValuesFromDynamic(entity, schema);
                if (keyValues.Count == 0 || keyValues.Values.All(v => v == null))
                    throw new ArgumentException($"Entity must have key properties ({string.Join(", ", keyColumns.Select(k => k.PropertyName))}) for bulk upsert");

                entityKeyValuePairs.Add((entity, keyValues));
            }

            var allKeysList = entityKeyValuePairs.Select(kvp => kvp.keyValues).ToList();
            var keyValuesForFetch = new List<object>();
            
            if (keyColumns.Count == 1)
            {
                var keyCol = keyColumns[0];
                keyValuesForFetch.AddRange(allKeysList.Select(kv => kv[keyCol.PropertyName]!));
            }
            else
            {
                keyValuesForFetch.AddRange(allKeysList.Cast<object>());
            }

            var existingRecords = await GetByIdsCoreAsync(entityType, keyValuesForFetch, cancellationToken);
            var existingDict = existingRecords.ToDictionary(e => GetKeyDisplayString(ExtractKeyValuesFromObject(e, schema)), e => e);

            var entitiesToInsert = new List<object>();
            var entitiesToUpdate = new List<object>();

            foreach (var (entityObj, keyValues) in entityKeyValuePairs)
            {
                var keyDisplay = GetKeyDisplayString(keyValues);
                if (existingDict.ContainsKey(keyDisplay))
                {
                    entitiesToUpdate.Add(entityObj);
                }
                else
                {
                    entitiesToInsert.Add(entityObj);
                }
            }

            var results = new List<object>();

            if (entitiesToInsert.Any())
            {
                var inserted = await BulkInsertCoreAsync(
                    entityType,
                    entitiesToInsert,
                    skipHooks: skipHooks,
                    skipChangeLog: skipChangeLog,
                    cancellationToken: cancellationToken);
                results.AddRange(inserted);
            }

            if (entitiesToUpdate.Any())
            {
                var updated = await BulkUpdateCoreAsync(
                    entityType,
                    entitiesToUpdate,
                    skipHooks: skipHooks,
                    ignoreInvariant: ignoreInvariant,
                    allowChangeInvariant: allowChangeInvariant,
                    skipChangeLog: skipChangeLog,
                    cancellationToken: cancellationToken);
                results.AddRange(updated);
            }

            return results;
        }

        protected override async Task<int> BulkDeleteCoreAsync(Type entityType, IEnumerable<object> ids, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);
            var idsList = ids.ToList();

            if (!idsList.Any())
                return 0;

            var keyColumns = schema.KeyColumns;
            if (keyColumns.Count == 0)
                throw new InvalidOperationException($"Cannot bulk delete {entityType.Name}: no key columns defined");

            var currentUserId = GetCurrentUserId();
            var currentUserDisplayName = await GetCurrentUserDisplayNameAsync(cancellationToken);
            var currentUser = GetCurrentUser();

            var keyValuesList = new List<Dictionary<string, object?>>();
            foreach (var id in idsList)
            {
                Dictionary<string, object?> keyValues;

                if (id is IDictionary<string, object?> keyDict)
                {
                    keyValues = new Dictionary<string, object?>(keyDict);
                }
                else if (keyColumns.Count == 1)
                {
                    var keyCol = keyColumns[0];
                    var convertedValue = ConvertKeyValue(id, keyCol.ClrType);
                    keyValues = new Dictionary<string, object?> { { keyCol.PropertyName, convertedValue } };
                }
                else
                {
                    throw new ArgumentException(
                        $"Composite key requires a dictionary with keys: {string.Join(", ", keyColumns.Select(k => k.PropertyName))}",
                        nameof(ids));
                }

                keyValuesList.Add(keyValues);
            }

            var entitiesToDelete = new List<TipsyBaboonModel>();
            foreach (var keyValues in keyValuesList)
            {
                var entity = await GetByIdCoreAsync(entityType, keyValues, cancellationToken) as TipsyBaboonModel;
                var keyDisplay = GetKeyDisplayString(keyValues);

                if (entity == null)
                    throw new InvalidOperationException($"Cannot delete {entityType.Name}: entity with key {keyDisplay} not found");

                if (!ignoreInvariant && entity.IsInvariant)
                    throw new InvalidOperationException($"Cannot delete {entityType.Name} with key {keyDisplay}: record is marked as invariant and cannot be deleted.");

                entitiesToDelete.Add(entity);
            }

            if (!skipHooks)
            {
                foreach (var entity in entitiesToDelete)
                {
                    if (entity is IModelWithSaveAction actionable)
                    {
                        var response = await actionable.BeforeChangeAsync(ActionType.Delete, _serviceProvider, currentUser, cancellationToken);

                        if (response == SaveResponseType.Error)
                        {
                            var keyValues = ExtractKeyValuesFromObject(entity, schema);
                            var keyDisplay = GetKeyDisplayString(keyValues);
                            throw new InvalidOperationException($"BeforeChangeAsync reported an error for {entityType.Name} with key {keyDisplay}");
                        }
                    }
                }
            }

            var tempTableName = $"#TempDelete_{Guid.NewGuid():N}";

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var createTempTableSql = new StringBuilder();
            createTempTableSql.AppendLine($"CREATE TABLE {tempTableName} (");

            var tempTableColumns = new List<string>();
            foreach (var keyCol in keyColumns)
            {
                var columnType = GetSqlServerType(keyCol);
                tempTableColumns.Add($"[{keyCol.ColumnName}] {columnType}");
            }
            createTempTableSql.AppendLine(string.Join(",\n", tempTableColumns));
            createTempTableSql.AppendLine(")");

            await using (var createCmd = new SqlCommand(createTempTableSql.ToString(), conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var dataTable = new DataTable();
            foreach (var keyCol in keyColumns)
            {
                var columnType = Nullable.GetUnderlyingType(keyCol.ClrType) ?? keyCol.ClrType;
                if (columnType.IsEnum)
                    columnType = typeof(int);
                else if (columnType == typeof(DateOnly))
                    columnType = typeof(DateTime);
                else if (columnType == typeof(TimeOnly))
                    columnType = typeof(TimeSpan);

                dataTable.Columns.Add(keyCol.ColumnName, columnType);
            }

            foreach (var entity in entitiesToDelete)
            {
                var row = dataTable.NewRow();
                foreach (var keyCol in keyColumns)
                {
                    var prop = entityType.GetProperty(keyCol.PropertyName);
                    var value = prop?.GetValue(entity);

                    if (value == null)
                    {
                        row[keyCol.ColumnName] = DBNull.Value;
                    }
                    else if (value is DateOnly dateOnly)
                    {
                        row[keyCol.ColumnName] = dateOnly.ToDateTime(TimeOnly.MinValue);
                    }
                    else if (value is TimeOnly timeOnly)
                    {
                        row[keyCol.ColumnName] = timeOnly.ToTimeSpan();
                    }
                    else if (value.GetType().IsEnum)
                    {
                        row[keyCol.ColumnName] = (int)value;
                    }
                    else
                    {
                        row[keyCol.ColumnName] = value;
                    }
                }
                dataTable.Rows.Add(row);
            }

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = tempTableName;
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300;

                foreach (var keyCol in keyColumns)
                {
                    bulkCopy.ColumnMappings.Add(keyCol.ColumnName, keyCol.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            }

            var deleteSql = new StringBuilder();
            deleteSql.AppendLine($"DELETE target FROM [dbo].[{sourceName}] AS target");
            deleteSql.AppendLine($"INNER JOIN {tempTableName} AS temp ON");
            var joinConditions = keyColumns.Select(k => $"target.[{k.ColumnName}] = temp.[{k.ColumnName}]");
            deleteSql.AppendLine(string.Join(" AND ", joinConditions));

            int deletedCount;
            await using (var deleteCmd = new SqlCommand(deleteSql.ToString(), conn))
            {
                deletedCount = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var dropCmd = new SqlCommand($"DROP TABLE {tempTableName}", conn))
            {
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!skipHooks)
            {
                foreach (var entity in entitiesToDelete)
                {
                    if (entity is IModelWithPostSaveAction postSaveActionable)
                    {
                        await postSaveActionable.AfterSaveAsync(ActionType.Delete, currentUser, cancellationToken);
                    }
                }
            }

            if (!skipChangeLog)
            {
                var changeLog = new ChangeLog
                {
                    ModuleName = schema.ModuleName,
                    ModelTypeName = entityType.Name,
                    RecordId = null,
                    RecordName = $"Bulk Delete of {entitiesToDelete.Count} records",
                    ActionType = ActionType.Delete,
                    ChangedById = currentUserId,
                    ChangedByDisplayName = currentUserDisplayName,
                    CreatedOn = DateTime.UtcNow,
                    CreatedById = currentUserId,
                    CreatedByDisplayName = currentUserDisplayName,
                    DescriptionOfChange = $"Bulk delete of {entitiesToDelete.Count} {entityType.Name} records"
                };

                await InsertCoreAsync(typeof(ChangeLog), changeLog, skipHooks: true, skipChangeLog: true, cancellationToken: cancellationToken);
            }

            return deletedCount;
        }

        protected override async Task<int> CountCoreAsync(Type entityType, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var sourceName = GetSourceName(schema);

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            // Filter permission views and governance tables to only count registered modules/models
            var whereClause = string.Empty;
            var cmd = new SqlCommand { Connection = conn };
            if (entityType.Name == "RolePermission" || entityType.Name == "ModelPermission")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames().ToList();
                if (registeredModules.Any())
                {
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModules.Count; i++)
                    {
                        var paramName = $"@module{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModules[i]);
                    }
                    whereClause = $" WHERE [ModuleName] IN ({string.Join(", ", inParams)})";
                }
            }
            else if (entityType.Name == "RADModule")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames().ToList();
                if (registeredModules.Any())
                {
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModules.Count; i++)
                    {
                        var paramName = $"@module{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModules[i]);
                    }
                    whereClause = $" WHERE [Name] IN ({string.Join(", ", inParams)})";
                }
            }
            else if (entityType.Name == "ModelRecord" || entityType.Name == "Privilege")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                var registeredModelIds = new List<Guid>();
                foreach (var moduleName in registeredModules)
                {
                    var moduleModels = BaseModelStore.GetAllSchemaDefinitions()
                        .Where(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                    registeredModelIds.AddRange(moduleModels.Select(m => m.RecordModelId));
                }
                
                if (registeredModelIds.Any())
                {
                    var filterColumn = entityType.Name == "Privilege" ? "RecordId" : "Id";
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModelIds.Count; i++)
                    {
                        var paramName = $"@modelId{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModelIds[i]);
                    }
                    whereClause = $" WHERE [{filterColumn}] IN ({string.Join(", ", inParams)})";
                }
            }
            else if (entityType.Name == "RolePrivilegeView")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                var registeredModelIds = new List<Guid>();
                foreach (var moduleName in registeredModules)
                {
                    var moduleModels = GetAllSchemaDefinitions()
                        .Where(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                    registeredModelIds.AddRange(moduleModels.Select(m => m.RecordModelId));
                }
                
                if (registeredModelIds.Any())
                {
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModelIds.Count; i++)
                    {
                        var paramName = $"@modelId{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModelIds[i]);
                    }
                    whereClause = $" WHERE [ModelId] IN ({string.Join(", ", inParams)})";
                }
            }

            var sql = $"SELECT COUNT(*) FROM [dbo].[{sourceName}]{whereClause}";
            cmd.CommandText = sql;

            return (int)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
        }

        protected override async Task<bool> ExistsCoreAsync(Type entityType, object id, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema(entityType)
                ?? throw new InvalidOperationException($"No schema found for type '{entityType.Name}'");
            var keyColumns = schema.KeyColumns;
            Dictionary<string, object?> keyValues;

            if (id is IDictionary<string, object?> keyDict)
            {
                keyValues = new Dictionary<string, object?>(keyDict);
            }
            else if (keyColumns.Count == 1)
            {
                var keyCol = keyColumns[0];
                var convertedValue = ConvertKeyValue(id, keyCol.ClrType);
                keyValues = new Dictionary<string, object?> { { keyCol.PropertyName, convertedValue } };
            }
            else
            {
                throw new ArgumentException(
                    $"Composite key requires a dictionary with keys: {string.Join(", ", keyColumns.Select(k => k.PropertyName))}",
                    nameof(id));
            }

            await using var conn = CreateConnection(entityType);
            await conn.OpenAsync(cancellationToken);

            var sourceName = GetSourceName(schema);
            var whereClause = BuildKeyWhereClause(schema, out _);
            var sql = $"SELECT COUNT(*) FROM [dbo].[{sourceName}]{whereClause}";

            await using var cmd = new SqlCommand(sql, conn);
            AddKeyParameters(cmd, schema, keyValues);

            return (int)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
        }

        protected override async Task<IEnumerable<object>> GetPagedCoreAsync(Type entityType, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var request = new PagedRequest
            {
                Page = pageNumber,
                PageSize = pageSize
            };
            var result = await QueryCoreAsync(entityType, request, cancellationToken);
            return result.Items;
        }

        #endregion

        #region BaseModelStore Abstract Method Implementations

        #region Single Entity

        public override Task<T?> GetTypedByIdAsync<T>(object id, CancellationToken cancellationToken = default) where T : class
            => GetByIdCoreAsync(typeof(T), id, cancellationToken).ContinueWith(t => (T?)t.Result, cancellationToken);

        protected override async Task<T> InsertTypedInternalAsync<T>(dynamic entity, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
            => (T)await InsertCoreAsync(typeof(T), entity, skipHooks, skipChangeLog, cancellationToken);

        protected override async Task<T> UpdateTypedInternalAsync<T>(dynamic entity, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
            => (T)await UpdateCoreAsync(typeof(T), entity, skipHooks, ignoreInvariant, allowChangeInvariant, skipChangeLog, cancellationToken);

        protected override async Task<T> UpsertTypedInternalAsync<T>(dynamic entity, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
            => (T)await UpsertCoreAsync(typeof(T), entity, skipHooks, ignoreInvariant, allowChangeInvariant, skipChangeLog, cancellationToken);

        protected override async Task<bool> DeleteTypedInternalAsync<T>(object id, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
            => await DeleteCoreAsync(typeof(T), id, skipHooks, ignoreInvariant, skipChangeLog, cancellationToken);

        #endregion

        #region Bulk Operations

        public override async Task<IEnumerable<T>> GetTypedByIdsAsync<T>(IEnumerable<object> ids, CancellationToken cancellationToken = default)
        {
            var results = await GetByIdsCoreAsync(typeof(T), ids, cancellationToken);
            return results.Cast<T>();
        }

        protected override async Task<IEnumerable<T>> BulkInsertTypedInternalAsync<T>(IEnumerable<dynamic> entities, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var results = await BulkInsertCoreAsync(typeof(T), entities.Cast<object>(), skipHooks, skipChangeLog, cancellationToken);
            return results.Cast<T>();
        }

        protected override async Task<IEnumerable<T>> BulkUpdateTypedInternalAsync<T>(IEnumerable<dynamic> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var results = await BulkUpdateCoreAsync(typeof(T), entities.Cast<object>(), skipHooks, ignoreInvariant, allowChangeInvariant, skipChangeLog, cancellationToken);
            return results.Cast<T>();
        }

        protected override async Task<IEnumerable<T>> BulkUpsertTypedInternalAsync<T>(IEnumerable<dynamic> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            var results = await BulkUpsertCoreAsync(typeof(T), entities.Cast<object>(), skipHooks, ignoreInvariant, allowChangeInvariant, skipChangeLog, cancellationToken);
            return results.Cast<T>();
        }

        public override async Task ClearOrphanedInvariantsAsync<T>(IEnumerable<T> validEntities, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema<T>();
            var keyColumns = schema.KeyColumns;
            if (keyColumns.Count == 0)
                return;

            var sourceName = GetSourceName(schema);
            var validEntitiesList = validEntities.ToList();

            if (validEntitiesList.Count == 0)
            {
                await using var conn = CreateConnection<T>();
                await conn.OpenAsync(cancellationToken);
                
                await using var clearAllCmd = new SqlCommand($"UPDATE [dbo].[{sourceName}] SET [IsInvariant] = 0 WHERE [IsInvariant] = 1", conn);
                await clearAllCmd.ExecuteNonQueryAsync(cancellationToken);
                return;
            }

            await using var connection = CreateConnection<T>();
            await connection.OpenAsync(cancellationToken);

            var cmd = new SqlCommand();
            cmd.Connection = connection;
            var paramIndex = 0;

            if (keyColumns.Count == 1)
            {
                var keyColumn = keyColumns[0];
                var values = validEntitiesList
                    .Select(r => typeof(T).GetProperty(keyColumn.PropertyName)?.GetValue(r))
                    .Where(v => v != null)
                    .ToList();

                var paramList = string.Join(", ", values.Select((_, i) => $"@inv{i}"));
                cmd.CommandText = $"UPDATE [dbo].[{sourceName}] SET [IsInvariant] = 0 WHERE [IsInvariant] = 1 AND [{keyColumn.ColumnName}] NOT IN ({paramList})";

                for (int i = 0; i < values.Count; i++)
                    cmd.Parameters.AddWithValue($"@inv{i}", values[i]!);
            }
            else
            {
                var conditions = new List<string>();
                foreach (var record in validEntitiesList)
                {
                    var parts = new List<string>();
                    foreach (var keyCol in keyColumns)
                    {
                        var value = typeof(T).GetProperty(keyCol.PropertyName)?.GetValue(record);
                        var paramName = $"@inv{paramIndex++}";
                        parts.Add($"[{keyCol.ColumnName}] = {paramName}");
                        cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
                    }
                    conditions.Add($"({string.Join(" AND ", parts)})");
                }
                cmd.CommandText = $"UPDATE [dbo].[{sourceName}] SET [IsInvariant] = 0 WHERE [IsInvariant] = 1 AND NOT ({string.Join(" OR ", conditions)})";
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            await cmd.DisposeAsync();
        }

        protected override async Task<int> BulkDeleteTypedInternalAsync<T>(IEnumerable<object> ids, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default)
        {
            return await BulkDeleteCoreAsync(typeof(T), ids, skipHooks, ignoreInvariant, skipChangeLog, cancellationToken);
        }

        #endregion

        #region Query Operations

        public override async Task<IEnumerable<T>> QueryTypedAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var schema = GetSchema<T>();
            await using var conn = CreateConnection<T>();
            await conn.OpenAsync(cancellationToken);

            var sourceName = GetSourceName(schema!);
            var columnList = string.Join(", ", schema!.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped).Select(c => $"[{c.ColumnName}]"));
            
            // Filter permission views and governance tables to only show registered modules/models
            var whereClause = string.Empty;
            var cmd = new SqlCommand { Connection = conn };
            if (typeof(T).Name == "RolePermission" || typeof(T).Name == "ModelPermission")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames().ToList();
                if (registeredModules.Any())
                {
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModules.Count; i++)
                    {
                        var paramName = $"@module{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModules[i]);
                    }
                    whereClause = $" WHERE [ModuleName] IN ({string.Join(", ", inParams)})";
                }
            }
            else if (typeof(T).Name == "RADModule")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames().ToList();
                if (registeredModules.Any())
                {
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModules.Count; i++)
                    {
                        var paramName = $"@module{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModules[i]);
                    }
                    whereClause = $" WHERE [Name] IN ({string.Join(", ", inParams)})";
                }
            }
            else if (typeof(T).Name == "ModelRecord" || typeof(T).Name == "Privilege")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                var registeredModelIds = new List<Guid>();
                foreach (var moduleName in registeredModules)
                {
                    var moduleModels = BaseModelStore.GetAllSchemaDefinitions()
                        .Where(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                    registeredModelIds.AddRange(moduleModels.Select(m => m.RecordModelId));
                }
                
                if (registeredModelIds.Any())
                {
                    var filterColumn = typeof(T).Name == "Privilege" ? "RecordId" : "Id";
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModelIds.Count; i++)
                    {
                        var paramName = $"@modelId{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModelIds[i]);
                    }
                    whereClause = $" WHERE [{filterColumn}] IN ({string.Join(", ", inParams)})";
                }
            }
            else if (typeof(T).Name == "RolePrivilegeView")
            {
                var registeredModules = ModelRegistry.GetRegisteredModuleNames();
                var registeredModelIds = new List<Guid>();
                foreach (var moduleName in registeredModules)
                {
                    var moduleModels = GetAllSchemaDefinitions()
                        .Where(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                    registeredModelIds.AddRange(moduleModels.Select(m => m.RecordModelId));
                }
                
                if (registeredModelIds.Any())
                {
                    var inParams = new List<string>();
                    for (int i = 0; i < registeredModelIds.Count; i++)
                    {
                        var paramName = $"@modelId{i}";
                        inParams.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, registeredModelIds[i]);
                    }
                    whereClause = $" WHERE [ModelId] IN ({string.Join(", ", inParams)})";
                }
            }
            
            var sql = $"SELECT {columnList} FROM [dbo].[{sourceName}]{whereClause}";
            cmd.CommandText = sql;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var results = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapEntity<T>(reader, schema));
            }

            var filtered = results.Where(predicate.Compile()).ToList();
            await LoadNavigationPropertiesForCollectionAsync(filtered, schema, inDetail: false, cancellationToken);
            return filtered;
        }

        #endregion

        #region Aggregate Operations

        public override async Task<int> CountTypedAsync<T>(CancellationToken cancellationToken = default)
        {
            return await CountCoreAsync(typeof(T), cancellationToken);
        }

        public override async Task<int> CountTypedAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var all = (await QueryTypedAsync<T>(PagedRequest.All, cancellationToken)).Items;
            return all.Count(predicate.Compile());
        }

        public override async Task<bool> ExistsTypedAsync<T>(object id, CancellationToken cancellationToken = default)
        {
            return await ExistsCoreAsync(typeof(T), id, cancellationToken);
        }

        public override async Task<bool> ExistsTypedAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var all = (await QueryTypedAsync<T>(PagedRequest.All, cancellationToken)).Items;
            return all.Any(predicate.Compile());
        }

        #endregion

        #region Paging Operations

        public override async Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var results = await GetPagedCoreAsync(typeof(T), pageNumber, pageSize, cancellationToken);
            return results.Cast<T>();
        }

        public override async Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var filtered = await QueryTypedAsync<T>(predicate, cancellationToken);
            return filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        public override async Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true, CancellationToken cancellationToken = default)
        {
            var all = (await QueryTypedAsync<T>(PagedRequest.All, cancellationToken)).Items;
            var ordered = ascending ? all.OrderBy(orderBy.Compile()) : all.OrderByDescending(orderBy.Compile());
            return ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        public override async Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate, Expression<Func<T, object>> orderBy, bool ascending = true, CancellationToken cancellationToken = default)
        {
            var filtered = await QueryTypedAsync<T>(predicate, cancellationToken);
            var ordered = ascending ? filtered.OrderBy(orderBy.Compile()) : filtered.OrderByDescending(orderBy.Compile());
            return ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        #endregion

        #region View Operations

        private readonly Dictionary<Type, string> _registeredViews = new();

        public override void RegisterView<TView>(string viewName)
        {
            _registeredViews[typeof(TView)] = viewName;
        }

        public override async Task<IEnumerable<T>> QueryTypedViewAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await QueryTypedAsync<T>(predicate, cancellationToken);
        }

        public override async Task<PagedResult<T>> QueryTypedViewAsync<T>(PagedRequest request, CancellationToken cancellationToken = default)
        {
            return await QueryTypedAsync<T>(request, cancellationToken);
        }

        public override bool IsViewRegistered<TView>()
        {
            return _registeredViews.ContainsKey(typeof(TView));
        }

        #endregion

        #endregion


        #region Private Helpers

        protected override Type? ResolveModelType(string moduleName, string modelName)
        {
            var schema = ModelRegistry.GetModel(modelName, moduleName);
            return schema?.ModelType;
        }

        private string? GetCurrentUser()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            return user.FindFirstValue(ClaimTypes.Name)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? user.Identity.Name;
        }

        private Guid? GetCurrentUserId()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }

        private async Task<string?> GetCurrentUserDisplayNameAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return GetCurrentUser();

            var currentUser = await GetTypedByIdAsync<User>(userId.Value, cancellationToken);
            var displayName = currentUser?.DisplayName ?? currentUser?.UserName ?? GetCurrentUser();

            var claimsPrincipal = _httpContextAccessor?.HttpContext?.User;
            var impersonationKey = claimsPrincipal?.FindFirstValue("ImpersonationKey");
            if (!string.IsNullOrEmpty(impersonationKey) && Guid.TryParse(impersonationKey, out var sessionKey))
            {
                var sessions = await QueryTypedAsync<ImpersonationSession>(
                    s => s.ImpersonationKey == sessionKey, cancellationToken);
                var session = sessions.FirstOrDefault();
                if (session != null)
                {
                    var impersonator = await GetTypedByIdAsync<User>(session.ImpersonatingUserId, cancellationToken);
                    if (impersonator != null)
                        displayName = $"{displayName} (impersonated by {impersonator.UserName})";
                }
            }

            return displayName;
        }

        private SqlConnection CreateConnection<T>() where T : TipsyBaboonModel
            => CreateConnection(typeof(T));

        private SqlConnection CreateConnection(Type entityType)
            => CreateConnection(ModelRegistry.GetModuleName(entityType));

        private SqlConnection CreateConnection(string moduleName)
        {
            var connString = ModelRegistry.GetConnectionString(moduleName);

            if (string.IsNullOrEmpty(connString))
            {
                throw new InvalidOperationException(
                    $"No connection string registered for module '{moduleName}'. " +
                    "Register the module via builder.AddModule() in services.AddTipsyBaboon().");
            }

            return new SqlConnection(connString);
        }

        private SchemaDefinition? GetSchema<T>() where T : TipsyBaboonModel
            => GetSchema(typeof(T));

        private SchemaDefinition? GetSchema(Type entityType)
        {
            return ModelRegistry.GetModel(entityType);
        }

        private string GetSourceName(SchemaDefinition schema)
        {
            return schema.IsViewModel ? $"vw_{schema.TypeName}" : schema.TableName;
        }

        private static string BuildFilterClause(
            ColumnDefinition column,
            FilterCriteria filter,
            ref int paramIndex,
            List<(string Name, object? Value)> parameters)
        {
            var colName = $"[{column.ColumnName}]";
            var paramName = $"@p{paramIndex++}";

            switch (filter.Operator)
            {
                case FilterOperator.Equals:
                    if (filter.Value == null)
                        return $"{colName} IS NULL";
                    parameters.Add((paramName, filter.Value));
                    return $"{colName} = {paramName}";

                case FilterOperator.NotEquals:
                    if (filter.Value == null)
                        return $"{colName} IS NOT NULL";
                    parameters.Add((paramName, filter.Value));
                    return $"{colName} <> {paramName}";

                case FilterOperator.GreaterThan:
                    parameters.Add((paramName, filter.Value));
                    return $"{colName} > {paramName}";

                case FilterOperator.GreaterThanOrEqual:
                    parameters.Add((paramName, filter.Value));
                    return $"{colName} >= {paramName}";

                case FilterOperator.LessThan:
                    parameters.Add((paramName, filter.Value));
                    return $"{colName} < {paramName}";

                case FilterOperator.LessThanOrEqual:
                    parameters.Add((paramName, filter.Value));
                    return $"{colName} <= {paramName}";

                case FilterOperator.Contains:
                    parameters.Add((paramName, $"%{filter.Value}%"));
                    return $"{colName} LIKE {paramName}";

                case FilterOperator.StartsWith:
                    parameters.Add((paramName, $"{filter.Value}%"));
                    return $"{colName} LIKE {paramName}";

                case FilterOperator.EndsWith:
                    parameters.Add((paramName, $"%{filter.Value}"));
                    return $"{colName} LIKE {paramName}";

                case FilterOperator.In:
                    if (filter.Value is System.Collections.IEnumerable enumerable)
                    {
                        var inParams = new List<string>();
                        foreach (var item in enumerable)
                        {
                            var inParamName = $"@p{paramIndex++}";
                            parameters.Add((inParamName, item));
                            inParams.Add(inParamName);
                        }
                        if (inParams.Count > 0)
                            return $"{colName} IN ({string.Join(", ", inParams)})";
                    }
                    return string.Empty;

                case FilterOperator.NotIn:
                    if (filter.Value is System.Collections.IEnumerable notInEnumerable)
                    {
                        var inParams = new List<string>();
                        foreach (var item in notInEnumerable)
                        {
                            var inParamName = $"@p{paramIndex++}";
                            parameters.Add((inParamName, item));
                            inParams.Add(inParamName);
                        }
                        if (inParams.Count > 0)
                            return $"{colName} NOT IN ({string.Join(", ", inParams)})";
                    }
                    return string.Empty;

                case FilterOperator.Between:
                    if (filter.Value is ValueTuple<object, object> range)
                    {
                        var lowerParam = $"@p{paramIndex++}";
                        var upperParam = $"@p{paramIndex++}";
                        parameters.Add((lowerParam, range.Item1));
                        parameters.Add((upperParam, range.Item2));
                        return $"{colName} BETWEEN {lowerParam} AND {upperParam}";
                    }
                    return string.Empty;

                case FilterOperator.IsNull:
                    return $"{colName} IS NULL";

                case FilterOperator.IsNotNull:
                    return $"{colName} IS NOT NULL";

                default:
                    return string.Empty;
            }
        }

        private string BuildOrderByClause(SchemaDefinition schema, List<SortCriteria> sorts)
        {
            if (sorts.Count == 0)
            {
                if (!string.IsNullOrEmpty(schema.RecordNameProperty))
                {
                    var recordNameColumn = schema.Columns.FirstOrDefault(c => c.PropertyName == schema.RecordNameProperty);
                    if (recordNameColumn != null)
                    {
                        return $"ORDER BY [{recordNameColumn.ColumnName}]";
                    }
                }
                var keyCols = schema.KeyColumns;
                if (keyCols.Count > 0)
                {
                    return $"ORDER BY {string.Join(", ", keyCols.Select(c => $"[{c.ColumnName}]"))}";
                }

                return $"ORDER BY [{schema.Columns.First().ColumnName}]";
            }

            var clauses = new List<string>();
            foreach (var sort in sorts)
            {
                var column = schema.Columns.FirstOrDefault(c =>
                    c.PropertyName.Equals(sort.PropertyName, StringComparison.OrdinalIgnoreCase) ||
                    c.ColumnName.Equals(sort.PropertyName, StringComparison.OrdinalIgnoreCase));

                if (column == null)
                {

                    continue;
                }

                var direction = sort.Direction == SortDirection.Descending ? "DESC" : "ASC";
                clauses.Add($"[{column.ColumnName}] {direction}");
            }

            if (clauses.Count == 0 && !string.IsNullOrEmpty(schema.RecordNameProperty))
            {
                var recordNameColumn = schema.Columns.FirstOrDefault(c => c.PropertyName == schema.RecordNameProperty);
                if (recordNameColumn != null)
                {
                    return $"ORDER BY [{recordNameColumn.ColumnName}]";
                }
            }

            return clauses.Count > 0 ? $"ORDER BY {string.Join(", ", clauses)}" : string.Empty;
        }

        private static void AddFilterParameters(SqlCommand cmd, List<(string Name, object? Value)> parameters)
        {
            foreach (var (name, value) in parameters)
            {
                if (value is DateTimeOffset dto)
                {
                    cmd.Parameters.Add(new SqlParameter(name, SqlDbType.DateTimeOffset) { Value = dto });
                }
                else
                {
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
                }
            }
        }

        #region Key Helpers

        private static string BuildKeyWhereClause(SchemaDefinition schema, out List<string> paramNames)
        {
            var keyColumns = schema.KeyColumns;
            paramNames = keyColumns.Select(k => k.PropertyName).ToList();
            var conditions = keyColumns.Select(k => $"[{k.ColumnName}] = @{k.PropertyName}");
            return " WHERE " + string.Join(" AND ", conditions);
        }

        private static void AddKeyParameters(SqlCommand cmd, SchemaDefinition schema, object keyValues)
        {
            var keyColumns = schema.KeyColumns;

            if (keyValues is IDictionary<string, object?> keyDict)
            {
                foreach (var keyCol in keyColumns)
                {
                    if (keyDict.TryGetValue(keyCol.PropertyName, out var value))
                    {
                        cmd.Parameters.AddWithValue($"@{keyCol.PropertyName}", value ?? DBNull.Value);
                    }
                    else
                    {
                        throw new ArgumentException($"Missing key value for '{keyCol.PropertyName}'", nameof(keyValues));
                    }
                }
            }
            else if (keyColumns.Count == 1)
            {
                var keyCol = keyColumns[0];
                var convertedValue = ConvertKeyValue(keyValues, keyCol.ClrType);
                cmd.Parameters.AddWithValue($"@{keyCol.PropertyName}", convertedValue ?? DBNull.Value);
            }
            else
            {
                throw new ArgumentException(
                    $"Composite key requires a dictionary with keys: {string.Join(", ", keyColumns.Select(k => k.PropertyName))}",
                    nameof(keyValues));
            }
        }

        private static object? ConvertKeyValue(object? value, Type targetType)
        {
            if (value == null) return null;

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(Guid))
            {
                if (value is Guid g) return g;
                if (Guid.TryParse(value.ToString(), out var parsed)) return parsed;
                throw new ArgumentException($"Cannot convert '{value}' to Guid");
            }

            if (underlying == typeof(int))
            {
                if (value is int i) return i;
                if (int.TryParse(value.ToString(), out var parsed)) return parsed;
                throw new ArgumentException($"Cannot convert '{value}' to int");
            }

            if (underlying == typeof(long))
            {
                if (value is long l) return l;
                if (long.TryParse(value.ToString(), out var parsed)) return parsed;
                throw new ArgumentException($"Cannot convert '{value}' to long");
            }

            if (underlying == typeof(string))
            {
                return value.ToString();
            }

            if (underlying.IsEnum)
            {
                if (value is string s) return Enum.Parse(underlying, s, ignoreCase: true);
                var enumUnderlyingType = Enum.GetUnderlyingType(underlying);
                var convertedValue = Convert.ChangeType(value, enumUnderlyingType);
                return Enum.ToObject(underlying, convertedValue);
            }

            return Convert.ChangeType(value, underlying);
        }

        private static Dictionary<string, object?> ExtractKeyValuesFromDynamic(dynamic entity, SchemaDefinition schema)
        {
            var result = new Dictionary<string, object?>();

            IDictionary<string, object?>? sourceDict = null;
            if (entity is IDictionary<string, object?> dict)
            {
                sourceDict = dict;
            }
            else if (entity is System.Dynamic.ExpandoObject expando)
            {
                sourceDict = (IDictionary<string, object?>)expando;
            }
            else if (entity is System.Text.Json.JsonElement jsonElement)
            {
                sourceDict = new Dictionary<string, object?>();
                foreach (var prop in jsonElement.EnumerateObject())
                {
                    sourceDict[prop.Name] = GetJsonValueStatic(prop.Value);
                }
            }
            else
            {
                sourceDict = new Dictionary<string, object?>();
                foreach (var prop in ((object)entity).GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    try { sourceDict[prop.Name] = prop.GetValue(entity); } catch { }
                }
            }

            foreach (var keyCol in schema.KeyColumns)
            {
                if (sourceDict.TryGetValue(keyCol.PropertyName, out var value))
                {
                    result[keyCol.PropertyName] = ConvertKeyValue(value, keyCol.ClrType);
                }
            }

            return result;
        }

        private static object? GetJsonValueStatic(System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.Object => element.Clone(),
                System.Text.Json.JsonValueKind.Array => element.Clone(),
                _ => element.GetRawText()
            };
        }

        private static string GetKeyDisplayString(Dictionary<string, object?> keyValues)
        {
            return string.Join(", ", keyValues.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        private static Dictionary<string, object?> ExtractKeyValuesFromObject(object entity, SchemaDefinition schema)
        {
            var result = new Dictionary<string, object?>();
            var entityType = entity.GetType();

            foreach (var keyCol in schema.KeyColumns)
            {
                var prop = entityType.GetProperty(keyCol.PropertyName);
                if (prop != null)
                {
                    result[keyCol.PropertyName] = prop.GetValue(entity);
                }
            }

            return result;
        }

        private async Task WriteChangeLogCoreAsync(TipsyBaboonModel entity, Type entityType, SchemaDefinition schema, ActionType action, string? description, CancellationToken cancellationToken)
        {
            if (entityType == typeof(ChangeLog)) return;

            var keyValues = ExtractKeyValuesFromObject(entity, schema);
            var keyDisplay = GetKeyDisplayString(keyValues);

            var userId = GetCurrentUserId();
            var displayName = await GetCurrentUserDisplayNameAsync(cancellationToken);

            var recordName = string.Empty;
            if (!string.IsNullOrEmpty(schema.RecordNameProperty))
            {
                var recordNameProp = entityType.GetProperty(schema.RecordNameProperty);
                if (recordNameProp != null)
                {
                    var recordNameValue = recordNameProp.GetValue(entity);
                    recordName = recordNameValue?.ToString() ?? string.Empty;
                }
            }

            var changeLog = new ChangeLog
            {
                ModuleName = schema.ModuleName,
                ModelTypeName = entityType.Name,
                RecordId = keyDisplay,
                RecordName = recordName,
                ActionType = action,
                ChangedById = userId,
                ChangedByDisplayName = displayName,
                CreatedOn = DateTime.UtcNow,
                CreatedById = userId,
                CreatedByDisplayName = displayName,
                DescriptionOfChange = description ?? $"{action} on {entityType.Name} [{keyDisplay}]"
            };

            await InsertCoreAsync(typeof(ChangeLog), changeLog, true, true, cancellationToken);
        }

        private async Task LoadNavigationPropertiesForCollectionUntypedAsync(IList<TipsyBaboonModel> entities, SchemaDefinition schema, bool inDetail, CancellationToken cancellationToken)
        {
            if (!entities.Any()) return;

            var viewStore = _viewModelStore.Value
                ?? throw new InvalidOperationException("IViewModelStore is not registered. Navigation properties require a view store.");

            var navProps = inDetail
                ? schema.Columns.Where(c => c.IsNavigationProperty && c.NavigationInDetail && c.IsNavigationViewModel && !string.IsNullOrEmpty(c.NavigationForeignKeyProperty)).ToList()
                : schema.Columns.Where(c => c.IsNavigationProperty && c.NavigationInGrid && c.IsNavigationViewModel && !string.IsNullOrEmpty(c.NavigationForeignKeyProperty)).ToList();

            if (!navProps.Any()) return;

            var entityDict = new Dictionary<object, TipsyBaboonModel>();
            foreach (var e in entities)
            {
                var keyVal = GetPrimaryKeyValue(e, schema);
                if (keyVal != null)
                    entityDict[keyVal] = e;
            }

            foreach (var navProp in navProps)
            {
                var allViewData = await GetViewDataAsync(navProp.NavigationElementType!, cancellationToken);
                if (allViewData == null) continue;

                var fkProp = navProp.NavigationElementType!.GetProperty(navProp.NavigationForeignKeyProperty!)
                    ?? throw new InvalidOperationException(
                        $"FK property '{navProp.NavigationForeignKeyProperty}' not found on type '{navProp.NavigationElementType.Name}'");

                var childrenByParent = new Dictionary<object, IList>();
                var listType = typeof(List<>).MakeGenericType(navProp.NavigationElementType);
                var addMethod = listType.GetMethod("Add");

                foreach (var item in allViewData)
                {
                    var fkValue = fkProp.GetValue(item);
                    if (fkValue != null && entityDict.ContainsKey(fkValue))
                    {
                        if (!childrenByParent.TryGetValue(fkValue, out var list))
                        {
                            list = Activator.CreateInstance(listType) as IList;
                            childrenByParent[fkValue] = list!;
                        }
                        addMethod!.Invoke(list, new[] { item });
                    }
                }

                var sampleEntity = entities.First();
                var entityPropInfo = sampleEntity.GetType().GetProperty(navProp.PropertyName)
                    ?? throw new InvalidOperationException(
                        $"Navigation property '{navProp.PropertyName}' not found on type '{sampleEntity.GetType().Name}'");
                foreach (var entity in entities)
                {
                    var entityKey = GetPrimaryKeyValue(entity, schema);
                    if (entityKey != null && childrenByParent.TryGetValue(entityKey, out var children))
                    {
                        entityPropInfo.SetValue(entity, children);

                        var childSchema = ModelRegistry.GetModel(navProp.NavigationElementType!);
                        if (childSchema != null && childSchema.Columns.Any(c => c.IsNavigationProperty))
                        {
                            foreach (var child in children)
                            {
                                if (child is TipsyBaboonModel childEntity)
                                {
                                    await LoadNavigationPropertiesForEntityAsync(childEntity, childSchema, inDetail, cancellationToken);
                                }
                            }
                        }
                    }
                    else
                    {
                        var emptyList = Activator.CreateInstance(listType) as IList;
                        entityPropInfo.SetValue(entity, emptyList);
                    }
                }
            }
        }

        private TipsyBaboonModel ApplyDynamicToEntity(object source, Type entityType, SchemaDefinition schema)
        {
            var target = Activator.CreateInstance(entityType) as TipsyBaboonModel
                ?? throw new InvalidOperationException($"Cannot create instance of {entityType.Name}");
            return ApplyDynamicToEntity(source, target, entityType, schema);
        }

        private TipsyBaboonModel ApplyDynamicToEntity(object source, TipsyBaboonModel existing, Type entityType, SchemaDefinition schema)
        {
            IDictionary<string, object?>? sourceDict = null;

            if (source is IDictionary<string, object?> dict)
            {
                sourceDict = dict;
            }
            else if (source is System.Dynamic.ExpandoObject expando)
            {
                sourceDict = (IDictionary<string, object?>)expando;
            }
            else if (source is System.Text.Json.JsonElement jsonElement)
            {
                sourceDict = new Dictionary<string, object?>();
                foreach (var prop in jsonElement.EnumerateObject())
                {
                    sourceDict[prop.Name] = GetJsonValueStatic(prop.Value);
                }
            }
            else
            {
                sourceDict = new Dictionary<string, object?>();
                foreach (var prop in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    sourceDict[prop.Name] = prop.GetValue(source);
                }
            }

            foreach (var kvp in sourceDict)
            {
                var targetProp = entityType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (targetProp != null && targetProp.CanWrite && kvp.Value != null)
                {
                    var convertedValue = ConvertToPropertyType(kvp.Value, targetProp.PropertyType);
                    targetProp.SetValue(existing, convertedValue);
                }
            }

            return existing;
        }

        #endregion

        #region Query Building

        private string BuildWhereClause(SchemaDefinition schema, List<FilterCriteria> filters, out List<(string Name, object? Value)> parameters)
        {
            parameters = new List<(string, object?)>();

            if (filters.Count == 0)
                return string.Empty;

            var clauses = new List<string>();
            var paramIndex = 0;

            foreach (var filter in filters)
            {
                var column = schema.Columns.FirstOrDefault(c =>
                    c.PropertyName.Equals(filter.PropertyName, StringComparison.OrdinalIgnoreCase) ||
                    c.ColumnName.Equals(filter.PropertyName, StringComparison.OrdinalIgnoreCase));

                if (column == null)
                {

                    continue;
                }

                var clause = BuildFilterClause(column, filter, ref paramIndex, parameters);
                if (!string.IsNullOrEmpty(clause))
                {
                    if (clauses.Count > 0)
                    {
                        clauses.Add(filter.Logic == LogicalOperator.Or ? "OR" : "AND");
                    }
                    clauses.Add(clause);
                }
            }

            return clauses.Count > 0 ? $" WHERE {string.Join(" ", clauses)}" : string.Empty;
        }

        #endregion

        #region Mapping Helpers

        private T MapEntity<T>(DbDataReader reader, SchemaDefinition schema) where T : TipsyBaboonModel
        {
            return (T)MapEntity(reader, typeof(T), schema);
        }

        private object MapEntity(DbDataReader reader, Type entityType, SchemaDefinition schema)
        {
            var entity = Activator.CreateInstance(entityType)!;

            foreach (var col in schema.Columns.Where(c => !c.IsNavigationProperty && !c.IsNotMapped))
            {
                var prop = entityType.GetProperty(col.PropertyName);
                if (prop == null) continue;

                var ordinal = reader.GetOrdinal(col.ColumnName);
                if (reader.IsDBNull(ordinal))
                {
                    if (col.IsNullable)
                    {
                        prop.SetValue(entity, null);
                    }
                    continue;
                }

                var value = reader.GetValue(ordinal);
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                if (targetType == typeof(Guid) && value is string str)
                {
                    prop.SetValue(entity, Guid.Parse(str));
                }
                else if (targetType == typeof(DateTimeOffset) && value is DateTime dt)
                {
                    prop.SetValue(entity, new DateTimeOffset(dt, TimeSpan.Zero));
                }
                else if (targetType == typeof(TimeOnly))
                {
                    if (value is TimeSpan ts)
                    {
                        prop.SetValue(entity, TimeOnly.FromTimeSpan(ts));
                    }
                    else if (value is DateTime dtTime)
                    {
                        prop.SetValue(entity, TimeOnly.FromDateTime(dtTime));
                    }
                    else if (value is string timeStr && TimeOnly.TryParse(timeStr, out var parsedTime))
                    {
                        prop.SetValue(entity, parsedTime);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Cannot convert value of type '{value.GetType().Name}' to TimeOnly for property '{col.PropertyName}'");
                    }
                }
                else if (targetType == typeof(DateOnly))
                {
                    if (value is DateTime dtDate)
                    {
                        prop.SetValue(entity, DateOnly.FromDateTime(dtDate));
                    }
                    else if (value is string dateStr && DateOnly.TryParse(dateStr, out var parsedDate))
                    {
                        prop.SetValue(entity, parsedDate);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Cannot convert value of type '{value.GetType().Name}' to DateOnly for property '{col.PropertyName}'");
                    }
                }
                else if (targetType.IsEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType(targetType);
                    var convertedValue = Convert.ChangeType(value, underlyingType);
                    prop.SetValue(entity, Enum.ToObject(targetType, convertedValue));
                }
                else
                {
                    prop.SetValue(entity, Convert.ChangeType(value, targetType));
                }
            }

            return entity;
        }

        private void AddParameters(SqlCommand cmd, object entity, Type entityType, List<ColumnDefinition> columns)
        {
            foreach (var col in columns)
            {
                var prop = entityType.GetProperty(col.PropertyName);
                if (prop == null) continue;

                var value = prop.GetValue(entity);

                if (value == null)
                {
                    cmd.Parameters.AddWithValue($"@{col.PropertyName}", DBNull.Value);
                }
                else if (value is DateTimeOffset dto)
                {
                    cmd.Parameters.Add(new SqlParameter($"@{col.PropertyName}", SqlDbType.DateTimeOffset) { Value = dto });
                }
                else if (value is TimeOnly timeOnly)
                {
                    cmd.Parameters.Add(new SqlParameter($"@{col.PropertyName}", SqlDbType.Time) { Value = timeOnly.ToTimeSpan() });
                }
                else if (value is DateOnly dateOnly)
                {
                    cmd.Parameters.Add(new SqlParameter($"@{col.PropertyName}", SqlDbType.Date) { Value = dateOnly.ToDateTime(TimeOnly.MinValue) });
                }
                else if (value.GetType().IsEnum)
                {
                    cmd.Parameters.AddWithValue($"@{col.PropertyName}", (int)value);
                }
                else
                {
                    cmd.Parameters.AddWithValue($"@{col.PropertyName}", value);
                }
            }
        }

        #endregion

        private static object? GetPrimaryKeyValue(object entity, SchemaDefinition schema)
        {
            if (schema.KeyColumns.Count == 0) return null;
            var prop = entity.GetType().GetProperty(schema.KeyColumns[0].PropertyName);
            return prop?.GetValue(entity);
        }

        private async Task LoadNavigationPropertiesForCollectionAsync<T>(IList<T> entities, SchemaDefinition schema, bool inDetail, CancellationToken cancellationToken) where T : TipsyBaboonModel
        {
            if (!entities.Any()) return;

            var viewStore = _viewModelStore.Value
                ?? throw new InvalidOperationException("IViewModelStore is not registered. Navigation properties require a view store.");

            var navProps = inDetail
                ? schema.Columns.Where(c => c.IsNavigationProperty && c.NavigationInDetail && c.IsNavigationViewModel && !string.IsNullOrEmpty(c.NavigationForeignKeyProperty)).ToList()
                : schema.Columns.Where(c => c.IsNavigationProperty && c.NavigationInGrid && c.IsNavigationViewModel && !string.IsNullOrEmpty(c.NavigationForeignKeyProperty)).ToList();

            if (!navProps.Any()) return;

            var entityDict = new Dictionary<object, T>();
            foreach (var e in entities)
            {
                var keyVal = GetPrimaryKeyValue(e, schema);
                if (keyVal != null)
                    entityDict[keyVal] = e;
            }

            foreach (var navProp in navProps)
            {
                var allViewData = await GetViewDataAsync(navProp.NavigationElementType!, cancellationToken);
                if (allViewData == null) continue;

                var fkProp = navProp.NavigationElementType!.GetProperty(navProp.NavigationForeignKeyProperty!)
                    ?? throw new InvalidOperationException(
                        $"FK property '{navProp.NavigationForeignKeyProperty}' not found on type '{navProp.NavigationElementType.Name}'");

                var childrenByParent = new Dictionary<object, IList>();
                var listType = typeof(List<>).MakeGenericType(navProp.NavigationElementType);
                var addMethod = listType.GetMethod("Add");

                foreach (var item in allViewData)
                {
                    var fkValue = fkProp.GetValue(item);
                    if (fkValue != null && entityDict.ContainsKey(fkValue))
                    {
                        if (!childrenByParent.TryGetValue(fkValue, out var list))
                        {
                            list = Activator.CreateInstance(listType) as IList;
                            childrenByParent[fkValue] = list!;
                        }
                        addMethod!.Invoke(list, new[] { item });
                    }
                }

                var entityPropInfo = typeof(T).GetProperty(navProp.PropertyName)
                    ?? throw new InvalidOperationException(
                        $"Navigation property '{navProp.PropertyName}' not found on type '{typeof(T).Name}'");
                foreach (var entity in entities)
                {
                    var entityKey = GetPrimaryKeyValue(entity, schema);
                    if (entityKey != null && childrenByParent.TryGetValue(entityKey, out var children))
                    {
                        entityPropInfo.SetValue(entity, children);

                        var childSchema = ModelRegistry.GetModel(navProp.NavigationElementType!);
                        if (childSchema != null && childSchema.Columns.Any(c => c.IsNavigationProperty))
                        {
                            foreach (var child in children)
                            {
                                if (child is TipsyBaboonModel childEntity)
                                {
                                    await LoadNavigationPropertiesForEntityAsync(childEntity, childSchema, inDetail, cancellationToken);
                                }
                            }
                        }
                    }
                    else
                    {
                        var emptyList = Activator.CreateInstance(listType) as IList;
                        entityPropInfo.SetValue(entity, emptyList);
                    }
                }
            }
        }

        private async Task LoadNavigationPropertiesForEntityAsync(TipsyBaboonModel entity, SchemaDefinition schema, bool inDetail, CancellationToken cancellationToken)
        {
            var viewStore = _viewModelStore.Value
                ?? throw new InvalidOperationException("IViewModelStore is not registered. Navigation properties require a view store.");

            var entityKeyValue = GetPrimaryKeyValue(entity, schema);
            if (entityKeyValue == null) return;

            var navProps = inDetail
                ? schema.Columns.Where(c => c.IsNavigationProperty && c.NavigationInDetail && c.IsNavigationViewModel && !string.IsNullOrEmpty(c.NavigationForeignKeyProperty)).ToList()
                : schema.Columns.Where(c => c.IsNavigationProperty && c.NavigationInGrid && c.IsNavigationViewModel && !string.IsNullOrEmpty(c.NavigationForeignKeyProperty)).ToList();

            if (!navProps.Any()) return;

            foreach (var navProp in navProps)
            {
                var allViewData = await GetViewDataAsync(navProp.NavigationElementType!, cancellationToken);
                if (allViewData == null) continue;

                var fkProp = navProp.NavigationElementType!.GetProperty(navProp.NavigationForeignKeyProperty!)
                    ?? throw new InvalidOperationException(
                        $"FK property '{navProp.NavigationForeignKeyProperty}' not found on type '{navProp.NavigationElementType.Name}'");

                var listType = typeof(List<>).MakeGenericType(navProp.NavigationElementType);
                var filteredList = Activator.CreateInstance(listType) as IList;
                var addMethod = listType.GetMethod("Add");

                foreach (var item in allViewData)
                {
                    var fkValue = fkProp.GetValue(item);
                    if (fkValue != null && fkValue.Equals(entityKeyValue))
                    {
                        addMethod!.Invoke(filteredList, new[] { item });

                        var childSchema = ModelRegistry.GetModel(navProp.NavigationElementType!);
                        if (childSchema != null && childSchema.Columns.Any(c => c.IsNavigationProperty))
                        {
                            if (item is TipsyBaboonModel childEntity)
                            {
                                await LoadNavigationPropertiesForEntityAsync(childEntity, childSchema, inDetail, cancellationToken);
                            }
                        }
                    }
                }

                var entityProp = entity.GetType().GetProperty(navProp.PropertyName)
                    ?? throw new InvalidOperationException(
                        $"Navigation property '{navProp.PropertyName}' not found on type '{entity.GetType().Name}'");
                entityProp.SetValue(entity, filteredList);
            }
        }

        private async Task<IEnumerable?> GetViewDataAsync(Type elementType, CancellationToken cancellationToken)
        {
            var viewStore = _viewModelStore.Value
                ?? throw new InvalidOperationException("IViewModelStore is not registered. View data requires a view store.");

            var getViewMethod = typeof(IViewModelStore).GetMethod(nameof(IViewModelStore.GetViewAsync))!
                .MakeGenericMethod(elementType);

            var task = (Task)getViewMethod.Invoke(viewStore, new object[] { cancellationToken })!;
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result")
                ?? throw new InvalidOperationException($"Result property not found on task for view type '{elementType.Name}'");
            return resultProperty.GetValue(task) as IEnumerable;
        }

        private object? ConvertToPropertyType(object value, Type targetType)
        {
            if (value == null) return null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == value.GetType()) return value;
            if (underlyingType.IsAssignableFrom(value.GetType())) return value;

            if (underlyingType == typeof(Guid))
            {
                if (value is string s && Guid.TryParse(s, out var g)) return g;
                if (value is Guid guid) return guid;
            }

            if (underlyingType == typeof(DateTimeOffset))
            {
                if (value is string s && DateTimeOffset.TryParse(s, out var dto)) return dto;
                if (value is DateTime dt) return new DateTimeOffset(dt);
                if (value is DateTimeOffset dateTimeOffset) return dateTimeOffset;
            }

            if (underlyingType == typeof(DateTime))
            {
                if (value is string s && DateTime.TryParse(s, out var dt)) return dt;
                if (value is DateTime dateTime) return dateTime;
            }

            if (underlyingType == typeof(TimeSpan))
            {
                if (value is TimeSpan ts) return ts;
                if (value is string s && TimeSpan.TryParse(s, out var parsedTs)) return parsedTs;
            }

            if (underlyingType == typeof(DateOnly))
            {
                if (value is DateOnly dateOnly) return dateOnly;
                if (value is string s && DateOnly.TryParse(s, out var parsedDate)) return parsedDate;
                if (value is DateTime dt) return DateOnly.FromDateTime(dt);
            }

            if (underlyingType == typeof(TimeOnly))
            {
                if (value is TimeOnly timeOnly) return timeOnly;
                if (value is string s && TimeOnly.TryParse(s, out var parsedTime)) return parsedTime;
                if (value is TimeSpan ts) return TimeOnly.FromTimeSpan(ts);
            }

            if (underlyingType.IsEnum)
            {
                if (value is string s) return Enum.Parse(underlyingType, s, ignoreCase: true);
                // Handle all numeric types that might come from JSON or dynamic objects
                if (value is int || value is long || value is short || value is byte ||
                    value is uint || value is ulong || value is ushort || value is sbyte)
                    return Enum.ToObject(underlyingType, value);
            }

            #region Numeric type conversion
            var isNumeric = underlyingType == typeof(int) || underlyingType == typeof(long) || underlyingType == typeof(short) || underlyingType == typeof(byte) ||
                            underlyingType == typeof(uint) || underlyingType == typeof(ulong) || underlyingType == typeof(ushort) || underlyingType == typeof(sbyte) ||
                            underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal);
            if (isNumeric)
            {
                return Convert.ChangeType(value, underlyingType);
            }
            #endregion

            if (underlyingType == typeof(bool))
            {
                if (value is string s) return bool.Parse(s);
                if (value is bool b) return b;
                if (value is int i) return i != 0;
                if (value is long l) return l != 0;
            }

            if (value is System.Text.Json.JsonElement jsonElement)
                return System.Text.Json.JsonSerializer.Deserialize(jsonElement.GetRawText(), underlyingType, _jsonOptions);

            #region Complex type JSON deserialization
            var isComplex = (underlyingType != typeof(string) && !underlyingType.IsPrimitive && !underlyingType.IsEnum &&
                            underlyingType != typeof(decimal) && underlyingType != typeof(Guid) &&
                            underlyingType != typeof(DateTime) && underlyingType != typeof(DateTimeOffset) &&
                            underlyingType != typeof(DateOnly) && underlyingType != typeof(TimeOnly) &&
                            underlyingType != typeof(TimeSpan)) &&
                            (underlyingType.IsClass || underlyingType.IsArray ||
                            (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() != typeof(Nullable<>)));
            if (value is string jsonString && isComplex)
                return System.Text.Json.JsonSerializer.Deserialize(jsonString, underlyingType, _jsonOptions);
            #endregion

            return Convert.ChangeType(value, underlyingType);
        }

        private static string GetSqlServerType(ColumnDefinition column)
        {
            var clrType = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
            var isNullable = column.ClrType != clrType || !column.ClrType.IsValueType;

            var sqlType = clrType switch
            {
                Type t when t == typeof(string) => column.MaxLength > 0 ? $"NVARCHAR({column.MaxLength})" : "NVARCHAR(MAX)",
                Type t when t == typeof(int) => "INT",
                Type t when t == typeof(long) => "BIGINT",
                Type t when t == typeof(short) => "SMALLINT",
                Type t when t == typeof(byte) => "TINYINT",
                Type t when t == typeof(bool) => "BIT",
                Type t when t == typeof(decimal) => "DECIMAL(18,2)",
                Type t when t == typeof(float) => "REAL",
                Type t when t == typeof(double) => "FLOAT",
                Type t when t == typeof(Guid) => "UNIQUEIDENTIFIER",
                Type t when t == typeof(DateTime) => "DATETIME2",
                Type t when t == typeof(DateTimeOffset) => "DATETIMEOFFSET",
                Type t when t == typeof(DateOnly) => "DATE",
                Type t when t == typeof(TimeOnly) => "TIME",
                Type t when t == typeof(TimeSpan) => "TIME",
                Type t when t.IsEnum => "INT",
                _ => "NVARCHAR(MAX)"
            };

            return isNullable ? $"{sqlType} NULL" : sqlType;
        }

        #endregion
    }
}
