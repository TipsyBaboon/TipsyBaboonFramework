using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Configuration;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.Core.Data;

namespace TipsyBaboon.SqlServer.Configuration
{
    /// <summary>
    /// SQL Server implementation of <see cref="IConfigurationPageService"/>.
    /// Stores module configuration as key-value pairs in the ConfigItem table using ADO.NET.
    /// Provides discovery of <see cref="Attributes.ConfigurationClassAttribute"/>-annotated classes,
    /// typed load/save operations, and individual property persistence.
    /// </summary>
    public class TipsyBaboonDatabaseConfigurationService : IConfigurationPageService
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;
        private readonly BaseModelStore _store;

        public TipsyBaboonDatabaseConfigurationService(
            IConfiguration config,
            BaseModelStore store)
        {
            _config = config;
            _connectionString = config.GetConnectionString("TipsyBaboonCore")
                ?? throw new InvalidOperationException("TipsyBaboonCore connection string not configured");
            _store = store;
        }

        internal async Task SaveConfigurationAsync<T>(T configuration, int precedence = 100, CancellationToken cancellationToken = default)
            where T : ConfigurationBase
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var moduleId = await GetModuleIdAsync(connection, configuration.ModuleName, cancellationToken);
            if (moduleId == null)
            {
                throw new InvalidOperationException($"Failed to get or create module {configuration.ModuleName}");
            }

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(ConfigurationBase.ModuleName) && p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                var key = prop.Name;
                var value = prop.GetValue(configuration);
                var valueType = prop.PropertyType.FullName ?? prop.PropertyType.Name;

                string serializedValue;
                if (value == null)
                {
                    serializedValue = string.Empty;
                }
                else if (IsComplexType(prop.PropertyType))
                {
                    serializedValue = JsonSerializer.Serialize(value);
                }
                else
                {
                    serializedValue = value.ToString() ?? string.Empty;
                }

                await UpsertConfigItemAsync(connection, moduleId.Value, key, serializedValue,
                    valueType, precedence, cancellationToken);
            }
        }

        public async Task<T> LoadConfigurationAsync<T>(CancellationToken cancellationToken = default)
            where T : ConfigurationBase, new()
        {
            var instance = new T();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var moduleId = await GetModuleIdAsync(connection, instance.ModuleName, cancellationToken);
            if (moduleId == null)
            {
                return instance;
            }

            var configItems = await LoadConfigItemsAsync(connection, moduleId.Value, cancellationToken);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(ConfigurationBase.ModuleName) && p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                var key = prop.Name;
                var configItem = configItems
                    .Where(c => c.Key == key)
                    .OrderByDescending(c => c.Precedence)
                    .FirstOrDefault();

                if (configItem != null && !string.IsNullOrWhiteSpace(configItem.Value))
                {
                    try
                    {
                        object? convertedValue;
                        if (IsComplexType(prop.PropertyType))
                        {
                            convertedValue = JsonSerializer.Deserialize(configItem.Value, prop.PropertyType);
                        }
                        else
                        {
                            convertedValue = ConvertValue(configItem.Value, prop.PropertyType);
                        }

                        prop.SetValue(instance, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to deserialize configuration value for property '{prop.Name}' on type '{typeof(T).Name}'", ex);
                    }
                }
            }

            return instance;
        }

        public async Task SaveConfigurationPropertyAsync<T>(string propertyName, object? value, int precedence = 100, CancellationToken cancellationToken = default)
            where T : ConfigurationBase, new()
        {
            var instance = new T();
            var prop = typeof(T).GetProperty(propertyName);
            
            if (prop == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(T).Name}'");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var moduleId = await GetModuleIdAsync(connection, instance.ModuleName, cancellationToken);
            if (moduleId == null)
            {
                throw new InvalidOperationException($"Failed to get or create module {instance.ModuleName}");
            }

            var valueType = prop.PropertyType.FullName ?? prop.PropertyType.Name;

            string serializedValue;
            if (value == null)
            {
                serializedValue = string.Empty;
            }
            else if (IsComplexType(prop.PropertyType))
            {
                serializedValue = JsonSerializer.Serialize(value);
            }
            else
            {
                serializedValue = value.ToString() ?? string.Empty;
            }

            await UpsertConfigItemAsync(connection, moduleId.Value, propertyName, serializedValue,
                valueType, precedence, cancellationToken);


        }

        public List<Type> DiscoverConfigurationTypes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !a.FullName!.StartsWith("System") && !a.FullName.StartsWith("Microsoft"));

            var configTypes = new List<Type>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ConfigurationBase)));
                    configTypes.AddRange(types);
                }
                catch
                {
                }
            }

            return configTypes.OrderBy(t => t.Name).ToList();
        }

        public async Task<List<ConfigurationPropertyInfo>> GetConfigurationPropertiesAsync<T>(CancellationToken cancellationToken = default)
            where T : ConfigurationBase, new()
        {
            var config = await LoadConfigurationAsync<T>(cancellationToken);
            var type = typeof(T);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.Name != nameof(ConfigurationBase.ModuleName))
                .OrderBy(p => p.GetCustomAttribute<UIDisplayAttribute>()?.Order ?? 999)
                .ToList();

            var result = new List<ConfigurationPropertyInfo>();

            foreach (var prop in properties)
            {
                var uiDisplay = prop.GetCustomAttribute<UIDisplayAttribute>();
                
                result.Add(new ConfigurationPropertyInfo
                {
                    Name = prop.Name,
                    DisplayName = uiDisplay?.Name ?? prop.Name,
                    Description = uiDisplay?.Description,
                    GroupName = uiDisplay?.GroupName ?? "General",
                    Order = uiDisplay?.Order ?? 999,
                    PropertyType = prop.PropertyType,
                    Value = prop.GetValue(config)
                });
            }

            return result;
        }

        private async Task<Guid?> GetModuleIdAsync(DbConnection connection, string moduleName, CancellationToken cancellationToken)
        {
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id FROM dbo.[RADModule] WHERE Name = @Name";
            var selectParam = selectCmd.CreateParameter();
            selectParam.ParameterName = "@Name";
            selectParam.Value = moduleName;
            selectCmd.Parameters.Add(selectParam);

            var result = await selectCmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                return (Guid)result;
            }

            throw new InvalidOperationException($"Module '{moduleName}' not found. Ensure the module is registered via AddModule() in Program.cs.");
        }

        private async Task<List<ConfigItem>> LoadConfigItemsAsync(DbConnection connection, Guid moduleId, CancellationToken cancellationToken)
        {
            var items = new List<ConfigItem>();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ModuleId, [Key], Value, ValueType, Precedence, IsRequired, Description, OwnershipLevel
                FROM dbo.ConfigItem
                WHERE ModuleId = @ModuleId";
            
            var param = cmd.CreateParameter();
            param.ParameterName = "@ModuleId";
            param.Value = moduleId;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new ConfigItem
                {
                    Id = reader.GetGuid(0),
                    ModuleId = reader.GetGuid(1),
                    Key = reader.GetString(2),
                    Value = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ValueType = reader.GetString(4),
                    Precedence = reader.GetInt32(5),
                    IsRequired = reader.GetBoolean(6),
                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                    OwnershipLevel = reader.IsDBNull(8) ? OwnershipLevel.Public : (OwnershipLevel)reader.GetInt32(8)
                });
            }

            return items;
        }

        private async Task UpsertConfigItemAsync(DbConnection connection, Guid moduleId, string key, string value,
            string valueType, int precedence, CancellationToken cancellationToken)
        {
            // Check if config item exists
            var existing = await _store.QueryTypedAsync<ConfigItem>(
                c => c.ModuleId == moduleId && c.Key == key && c.Precedence == precedence,
                cancellationToken);
            
            var existingItem = existing.FirstOrDefault();

            if (existingItem != null)
            {
                // Update existing config using ModelStore (handles all metadata)
                existingItem.Value = value;
                existingItem.ValueType = valueType;
                await _store.UpdateTypedAsync<ConfigItem>(existingItem, cancellationToken);
            }
            else
            {
                // Insert new config using ModelStore (handles all metadata)
                var newItem = new ConfigItem
                {
                    Id = Guid.NewGuid(),
                    ModuleId = moduleId,
                    Key = key,
                    Value = value,
                    ValueType = valueType,
                    Precedence = precedence,
                    IsRequired = false,
                    OwnershipLevel = OwnershipLevel.Public
                };
                await _store.InsertTypedAsync<ConfigItem>(newItem, cancellationToken);
            }
        }

        private bool IsComplexType(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            if (underlyingType == typeof(string))
                return false;

            if (underlyingType.IsArray)
                return true;

            if (underlyingType.IsGenericType)
            {
                var genericTypeDef = underlyingType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(List<>) ||
                    genericTypeDef == typeof(IList<>) ||
                    genericTypeDef == typeof(ICollection<>) ||
                    genericTypeDef == typeof(IEnumerable<>))
                {
                    return true;
                }
            }

            if (underlyingType.IsClass || underlyingType.IsInterface)
                return true;

            return false;
        }

        private object? ConvertValue(string value, Type targetType)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(string))
                return value;
            if (underlyingType == typeof(int))
                return int.Parse(value);
            if (underlyingType == typeof(long))
                return long.Parse(value);
            if (underlyingType == typeof(bool))
                return bool.Parse(value);
            if (underlyingType == typeof(decimal))
                return decimal.Parse(value);
            if (underlyingType == typeof(double))
                return double.Parse(value);
            if (underlyingType == typeof(DateTime))
                return DateTime.Parse(value);
            if (underlyingType == typeof(Guid))
                return Guid.Parse(value);
            
            if (underlyingType.IsEnum)
                return Enum.Parse(underlyingType, value, ignoreCase: true);

            return Convert.ChangeType(value, underlyingType);
        }
    }
}
