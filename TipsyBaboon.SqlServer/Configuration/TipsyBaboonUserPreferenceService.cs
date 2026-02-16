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
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.SqlServer.Configuration
{
    /// <summary>
    /// SQL Server implementation of IUserPreferenceService for per-user preference storage.
    /// Stores preferences in UserPreference table with UserId/Key/Value structure.
    /// </summary>
    /// <remarks>
    /// UserPreference table schema:
    /// - UserId: Guid linking to User table
    /// - Key: Preference property name
    /// - Value: Serialized value (JSON for complex types, ToString for simple types)
    /// 
    /// Uses MERGE for atomic upserts.
    /// Supports discovery of types marked with [UserPreferenceClass] attribute.
    /// </remarks>
    public class TipsyBaboonUserPreferenceService : IUserPreferenceService
    {
        private readonly string _connectionString;
        private readonly BaseModelStore _store;

        public TipsyBaboonUserPreferenceService(IConfiguration config, BaseModelStore store)
        {
            _connectionString = config.GetConnectionString("TipsyBaboonCore")
                ?? throw new InvalidOperationException("TipsyBaboonCore connection string not configured");
            _store = store;
        }

        /// <summary>
        /// Discovers all types in loaded assemblies marked with [UserPreferenceClass] attribute.
        /// </summary>
        /// <returns>List of types that represent user preference classes</returns>
        /// <remarks>
        /// Scans all loaded assemblies in current AppDomain.
        /// Filters to classes (not abstract) with UserPreferenceClassAttribute.
        /// Safe: catches and ignores assembly load failures.
        /// Used by framework to auto-register preference pages.
        /// </remarks>
        public List<Type> DiscoverUserPreferenceTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<UserPreferenceClassAttribute>() != null)
                .ToList();
        }

        /// <summary>
        /// Loads user preferences for a specific user into a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">User preference type inheriting from UserPreferenceBase</typeparam>
        /// <param name="userId">User ID to load preferences for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Preference instance with properties populated from UserPreference table</returns>
        /// <remarks>
        /// Creates a new T instance and populates properties from UserPreference records.
        /// Deserializes JSON for complex types, converts strings for simple types.
        /// Returns default instance (parameterless constructor) if no preferences stored.
        /// Silently skips properties that fail conversion.
        /// </remarks>
        public async Task<T> LoadUserPreferencesAsync<T>(Guid userId, CancellationToken cancellationToken = default)
            where T : UserPreferenceBase, new()
        {
            var instance = new T();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var items = await LoadUserPreferenceItemsAsync(connection, userId, cancellationToken);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(UserPreferenceBase.ModuleName) && p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                var item = items.FirstOrDefault(i => i.Key == prop.Name);
                if (item != null && !string.IsNullOrEmpty(item.Value))
                {
                    try
                    {
                        var convertedValue = ConvertValue(item.Value, prop.PropertyType);
                        prop.SetValue(instance, convertedValue);
                    }
                    catch
                    {
                    }
                }
            }

            return instance;
        }

        /// <summary>
        /// Saves a single user preference property to the UserPreference table.
        /// </summary>
        /// <typeparam name="T">User preference type inheriting from UserPreferenceBase (used to validate property exists)</typeparam>
        /// <param name="userId">User ID to save preference for</param>
        /// <param name="propertyName">Property name to save</param>
        /// <param name="value">Property value (null allowed)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <exception cref="ArgumentException">Thrown if property doesn't exist on type T</exception>
        /// <remarks>
        /// Serializes complex types to JSON, simple types to string.
        /// Uses MERGE for atomic upsert.
        /// </remarks>
        public async Task SaveUserPreferencePropertyAsync<T>(Guid userId, string propertyName, object? value, CancellationToken cancellationToken = default)
            where T : UserPreferenceBase, new()
        {
            var prop = typeof(T).GetProperty(propertyName);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(T).Name}'");

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

            await SetPreferenceAsync(userId, propertyName, serializedValue, cancellationToken);
        }

        /// <summary>
        /// Retrieves a single user preference value by key.
        /// </summary>
        /// <param name="userId">User ID to retrieve preference for</param>
        /// <param name="key">Preference key (property name)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Preference value as string, or null if not found</returns>
        /// <remarks>
        /// Returns raw stored value (JSON for complex types, ToString result for simple types).
        /// Caller must deserialize/convert as needed.
        /// </remarks>
        public async Task<string?> GetPreferenceAsync(Guid userId, string key, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Value FROM dbo.UserPreferenceItem WHERE UserId = @UserId AND [Key] = @Key";
            
            var userIdParam = cmd.CreateParameter();
            userIdParam.ParameterName = "@UserId";
            userIdParam.Value = userId;
            cmd.Parameters.Add(userIdParam);

            var keyParam = cmd.CreateParameter();
            keyParam.ParameterName = "@Key";
            keyParam.Value = key;
            cmd.Parameters.Add(keyParam);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
                return null;

            return result.ToString();
        }

        /// <summary>
        /// Sets a single user preference value by key.
        /// </summary>
        /// <param name="userId">User ID to save preference for</param>
        /// <param name="key">Preference key (property name)</param>
        /// <param name="value">Preference value (null allowed)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <remarks>
        /// Upserts UserPreference record using MERGE.
        /// Value should be pre-serialized if complex type.
        /// </remarks>
        public async Task SetPreferenceAsync(Guid userId, string key, string? value, CancellationToken cancellationToken = default)
        {
            // Check if preference exists
            var existing = await _store.QueryTypedAsync<UserPreferenceItem>(
                p => p.UserId == userId && p.Key == key, 
                cancellationToken);
            
            var existingItem = existing.FirstOrDefault();

            if (existingItem != null)
            {
                // Update existing preference using ModelStore (handles all metadata)
                existingItem.Value = value;
                await _store.UpdateTypedAsync<UserPreferenceItem>(existingItem, cancellationToken);
            }
            else
            {
                // Insert new preference using ModelStore (handles all metadata)
                var newItem = new UserPreferenceItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Key = key,
                    Value = value,
                    OwnershipLevel = Core.OwnershipLevel.Private
                };
                await _store.InsertTypedAsync<UserPreferenceItem>(newItem, cancellationToken);
            }
        }

        private async Task<List<PreferenceItem>> LoadUserPreferenceItemsAsync(DbConnection connection, Guid userId, CancellationToken cancellationToken)
        {
            var items = new List<PreferenceItem>();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT [Key], Value FROM dbo.UserPreferenceItem WHERE UserId = @UserId";

            var param = cmd.CreateParameter();
            param.ParameterName = "@UserId";
            param.Value = userId;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new PreferenceItem
                {
                    Key = reader.GetString(0),
                    Value = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }

            return items;
        }

        private static object? ConvertValue(string value, Type targetType)
        {
            if (string.IsNullOrEmpty(value))
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(bool))
                return bool.Parse(value);
            if (underlyingType == typeof(int))
                return int.Parse(value);
            if (underlyingType == typeof(long))
                return long.Parse(value);
            if (underlyingType == typeof(decimal))
                return decimal.Parse(value);
            if (underlyingType == typeof(double))
                return double.Parse(value);
            if (underlyingType == typeof(Guid))
                return Guid.Parse(value);
            if (underlyingType.IsEnum)
                return Enum.Parse(underlyingType, value);

            if (IsComplexType(targetType))
                return JsonSerializer.Deserialize(value, targetType);

            return value;
        }

        private static bool IsComplexType(Type type)
        {
            return !type.IsPrimitive
                && type != typeof(string)
                && type != typeof(decimal)
                && type != typeof(DateTime)
                && type != typeof(DateTimeOffset)
                && type != typeof(TimeSpan)
                && type != typeof(Guid)
                && !type.IsEnum
                && Nullable.GetUnderlyingType(type) == null;
        }

        private class PreferenceItem
        {
            public string Key { get; set; } = string.Empty;
            public string? Value { get; set; }
        }
    }
}
