using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.Core.FrameworkBase;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TipsyBaboon.Core.Attributes;
using GovernanceModule = TipsyBaboon.Core.Models.Governance.RADModule;

namespace TipsyBaboonTestSite.Services
{
    public class AppConfigurationManager
    {
        private readonly BaseModelStore _store;
        private readonly IMemoryCache _cache;
        private const string CacheKeyPrefix = "Config:";
        private const int CacheExpirationMinutes = 30;

        public AppConfigurationManager(
            BaseModelStore store,
            IMemoryCache cache)
        {
            _store = store;
            _cache = cache;
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

        public async Task<T> LoadConfigurationAsync<T>() where T : ConfigurationBase
        {
            var type = typeof(T);
            var cacheKey = $"{CacheKeyPrefix}{type.FullName}";

            if (_cache.TryGetValue<T>(cacheKey, out var cached))
                return cached!;

            var config = (T)Activator.CreateInstance(type)!;
            var moduleName = config.ModuleName;

            var modules = await _store.QueryTypedAsync<GovernanceModule>(m => m.Name == moduleName);
            var module = modules.FirstOrDefault();

            if (module == null)
            {
                _cache.Set(cacheKey, config, TimeSpan.FromMinutes(CacheExpirationMinutes));
                return config;
            }

            var allConfigItems = await _store.QueryTypedAsync<ConfigItem>(c => c.ModuleId == module.Id);
            var configItems = allConfigItems.ToList();

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetCustomAttribute<UIDisplayAttribute>() != null);

            foreach (var prop in properties)
            {
                var configItem = configItems.FirstOrDefault(c => c.Key == prop.Name);
                if (configItem != null && !string.IsNullOrEmpty(configItem.Value))
                {
                    try
                    {
                        var value = DeserializeValue(configItem.Value, prop.PropertyType);
                        prop.SetValue(config, value);
                    }
                    catch
                    {
                    }
                }
            }

            _cache.Set(cacheKey, config, TimeSpan.FromMinutes(CacheExpirationMinutes));
            return config;
        }

        public async Task SaveConfigurationPropertyAsync<T>(string propertyName, object? value) where T : ConfigurationBase
        {
            var type = typeof(T);
            var config = (T)Activator.CreateInstance(type)!;
            var moduleName = config.ModuleName;

            var modules = await _store.QueryTypedAsync<GovernanceModule>(m => m.Name == moduleName);
            var module = modules.FirstOrDefault();

            if (module == null)
            {
                module = new GovernanceModule
                {
                    Id = Guid.NewGuid(),
                    Name = moduleName
                };
                await _store.InsertTypedAsync<GovernanceModule>(module);
            }

            var configItems = await _store.QueryTypedAsync<ConfigItem>(c => c.ModuleId == module.Id && c.Key == propertyName);
            var configItem = configItems.FirstOrDefault();

            var serializedValue = SerializeValue(value);

            if (configItem == null)
            {
                configItem = new ConfigItem
                {
                    Id = Guid.NewGuid(),
                    ModuleId = module.Id,
                    Key = propertyName,
                    Value = serializedValue,
                    ValueType = value?.GetType().FullName ?? "System.String",
                    Precedence = 0,
                    OwnershipLevel = TipsyBaboon.Core.OwnershipLevel.Public
                };
                await _store.InsertTypedAsync<ConfigItem>(configItem);
            }
            else
            {
                configItem.Value = serializedValue;
                configItem.ValueType = value?.GetType().FullName ?? "System.String";
                configItem.ModifiedOn = DateTime.UtcNow;
                await _store.UpdateTypedAsync<ConfigItem>(configItem);
            }

            var cacheKey = $"{CacheKeyPrefix}{type.FullName}";
            _cache.Remove(cacheKey);
        }

        public async Task<List<ConfigurationProperty>> GetConfigurationPropertiesAsync<T>() where T : ConfigurationBase
        {
            var config = await LoadConfigurationAsync<T>();
            var type = typeof(T);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetCustomAttribute<UIDisplayAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<UIDisplayAttribute>()?.Order ?? 999)
                .ToList();

            var result = new List<ConfigurationProperty>();

            foreach (var prop in properties)
            {
                var uiDisplay = prop.GetCustomAttribute<UIDisplayAttribute>();
                result.Add(new ConfigurationProperty
                {
                    Name = prop.Name,
                    DisplayName = uiDisplay?.Name ?? prop.Name,
                    Description = uiDisplay?.Description,
                    GroupName = uiDisplay?.GroupName ?? "General",
                    Value = prop.GetValue(config),
                    PropertyType = prop.PropertyType,
                    Order = uiDisplay?.Order ?? 999
                });
            }

            return result;
        }

        private string SerializeValue(object? value)
        {
            if (value == null) return string.Empty;
            if (value is string str) return str;
            return JsonSerializer.Serialize(value);
        }

        private object? DeserializeValue(string serialized, Type targetType)
        {
            if (string.IsNullOrEmpty(serialized)) return null;
            if (targetType == typeof(string)) return serialized;
            
            if (targetType == typeof(bool)) return bool.Parse(serialized);
            if (targetType == typeof(int)) return int.Parse(serialized);
            if (targetType == typeof(long)) return long.Parse(serialized);
            if (targetType == typeof(decimal)) return decimal.Parse(serialized);
            if (targetType == typeof(double)) return double.Parse(serialized);
            
            return JsonSerializer.Deserialize(serialized, targetType);
        }
    }

    public class ConfigurationProperty
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string GroupName { get; set; } = "General";
        public object? Value { get; set; }
        public Type PropertyType { get; set; } = typeof(string);
        public int Order { get; set; }
    }
}
