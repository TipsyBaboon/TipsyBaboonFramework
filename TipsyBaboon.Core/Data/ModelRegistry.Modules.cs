using System;
using System.Collections.Generic;
using System.Linq;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.Core.Data
{
    public static partial class ModelRegistry
    {
        private static readonly Dictionary<string, RegisteredModule> _modules = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a module with its connection string and optional description.
        /// Must be called during application startup for each module (typically via TipsyBaboonBuilder.AddModule).
        /// </summary>
        /// <param name="moduleName">Unique module name (e.g., "Governance", "TipsyBaboonFishing").</param>
        /// <param name="connectionString">Database connection string for this module.</param>
        /// <param name="description">Optional module description for documentation purposes.</param>
        public static void Register(string moduleName, string connectionString, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name is required", nameof(moduleName));

            _modules[moduleName] = new RegisteredModule
            {
                Name = moduleName,
                ConnectionString = connectionString,
                Description = description,
                RegisteredAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Checks whether a module has been registered.
        /// </summary>
        /// <param name="moduleName">Module name to check.</param>
        /// <returns>True if the module is registered, otherwise false.</returns>
        public static bool IsRegistered(string moduleName)
        {
            return _modules.ContainsKey(moduleName);
        }

        /// <summary>
        /// Retrieves the connection string for a registered module.
        /// </summary>
        /// <param name="moduleName">Module name.</param>
        /// <returns>Connection string if module is registered, otherwise null.</returns>
        public static string? GetConnectionString(string moduleName)
        {
            return _modules.TryGetValue(moduleName, out var m) ? m.ConnectionString : null;
        }

        /// <summary>
        /// Gets all registered modules.
        /// </summary>
        /// <returns>Read-only list of RegisteredModule instances.</returns>
        public static IReadOnlyList<RegisteredModule> GetAllModules()
        {
            return _modules.Values.ToList();
        }

        internal static IReadOnlySet<string> GetRegisteredModuleNames()
        {
            return _modules.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds an invariant record to be upserted on every application startup.
        /// Invariant records are system-locked and have IsInvariant=true.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="record">Record instance to add as invariant.</param>
        public static void AddInvariant<T>(T record) where T : class
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var typeName = typeof(T).FullName ?? typeof(T).Name;
            var module = FindModuleForType(typeName);
            if (module == null)
                throw new InvalidOperationException($"No module registered for type {typeName}. Ensure AddModule() is called before adding invariants.");

            module.ManualInvariants.Add(record);
        }

        /// <summary>
        /// Adds an invariant configuration key-value pair to a specific module.
        /// Invariant configs are upserted on every startup.
        /// </summary>
        /// <param name="moduleName">Target module name.</param>
        /// <param name="key">Configuration key (property name).</param>
        /// <param name="value">Configuration value.</param>
        public static void AddInvariantConfigToModule(string moduleName, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name is required", nameof(moduleName));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Config key is required", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (!_modules.TryGetValue(moduleName, out var module))
                throw new InvalidOperationException($"Module '{moduleName}' not registered. Ensure AddModule() is called first.");

            module.ManualInvariantConfigs[key] = value;
            
            // Populate static configuration instance if a matching configuration class exists
            PopulateStaticConfiguration(moduleName, key, value);
        }
        
        private static void PopulateStaticConfiguration(string moduleName, string key, string value)
        {
            // Look for {ModuleName}Configuration class with SetValue(propertyName, value) method
            // e.g., "TipsyBaboon.Core" -> "TipsyBaboonCore.Configuration.CoreConfiguration"
            var configClassName = $"{moduleName.Replace(".", "")}.Configuration.{moduleName.Replace("TipsyBaboon.", "")}Configuration";
            var configType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == configClassName);
                
            if (configType != null)
            {
                var setValueMethod = configType.GetMethod("SetValue", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Configuration class '{configClassName}' does not have a static 'SetValue' method");
                setValueMethod.Invoke(null, new object[] { key, value });
            }
        }

        /// <summary>
        /// Adds a seed record to be inserted once at table creation only (not maintained).
        /// Seed records are not upserted on subsequent startups.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="record">Record instance to seed.</param>
        public static void AddSeed<T>(T record) where T : class
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var typeName = typeof(T).FullName ?? typeof(T).Name;
            var module = FindModuleForType(typeName);
            if (module == null)
                throw new InvalidOperationException($"No module registered for type {typeName}. Ensure AddModule() is called before adding seeds.");

            module.ManualSeeds.Add(record);
        }

        /// <summary>
        /// Adds a seed configuration key-value pair that is inserted once at table creation for the specified module.
        /// Unlike invariant configs, seed configs are not maintained on subsequent startups.
        /// </summary>
        /// <param name="moduleName">Target module name.</param>
        /// <param name="key">Configuration key.</param>
        /// <param name="value">Configuration value.</param>
        public static void AddSeedConfigToModule(string moduleName, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name is required", nameof(moduleName));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Config key is required", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (!_modules.TryGetValue(moduleName, out var module))
                throw new InvalidOperationException($"Module '{moduleName}' not registered. Ensure AddModule() is called first.");

            module.ManualSeedConfigs[key] = value;
        }

        private static RegisteredModule? FindModuleForType(string typeName)
        {
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
                
                if (type == null)
                    return null;
                
                var moduleName = GetModuleName(type);
                if (string.IsNullOrWhiteSpace(moduleName))
                    return null;
                
                return _modules.TryGetValue(moduleName, out var module) ? module : null;
            }
            catch
            {
                return null;
            }
        }

        internal static IReadOnlyList<object> GetAllManualInvariants()
        {
            return _modules.Values.SelectMany(m => m.ManualInvariants).ToList();
        }

        internal static IReadOnlyDictionary<string, string> GetManualInvariantConfigsForModule(string moduleName)
        {
            if (_modules.TryGetValue(moduleName, out var module))
            {
                return module.ManualInvariantConfigs;
            }
            return new Dictionary<string, string>();
        }
        
        internal static IReadOnlyDictionary<string, string> GetManualSeedConfigsForModule(string moduleName)
        {
            if (_modules.TryGetValue(moduleName, out var module))
            {
                return module.ManualSeedConfigs;
            }
            return new Dictionary<string, string>();
        }

        internal static IReadOnlyList<object> GetAllManualSeeds()
        {
            return _modules.Values.SelectMany(m => m.ManualSeeds).ToList();
        }


        private static readonly List<PermissionRequest> _seedPermissions = new();
        private static readonly List<PermissionRequest> _invariantPermissions = new();

        /// <summary>
        /// Registers a seed permission to be created once when the Permission table is first created.
        /// Used by consumer modules to declare initial RBAC permissions for their models.
        /// </summary>
        public static void AddSeedPermission(string roleName, string moduleName, string modelName,
            bool usePages, bool canCreate, PermissionLevel readLevel, PermissionLevel updateLevel, PermissionLevel deleteLevel)
        {
            _seedPermissions.Add(new PermissionRequest
            {
                RoleName = roleName,
                ModuleName = moduleName,
                ModelName = modelName,
                UsePages = usePages,
                CanCreate = canCreate,
                ReadLevel = readLevel,
                UpdateLevel = updateLevel,
                DeleteLevel = deleteLevel
            });
        }

        /// <summary>
        /// Registers an invariant permission that is upserted on every startup.
        /// Used for system-critical permissions that must always exist regardless of user changes.
        /// </summary>
        public static void AddInvariantPermission(string roleName, string moduleName, string modelName,
            bool usePages, bool canCreate, PermissionLevel readLevel, PermissionLevel updateLevel, PermissionLevel deleteLevel)
        {
            _invariantPermissions.Add(new PermissionRequest
            {
                RoleName = roleName,
                ModuleName = moduleName,
                ModelName = modelName,
                UsePages = usePages,
                CanCreate = canCreate,
                ReadLevel = readLevel,
                UpdateLevel = updateLevel,
                DeleteLevel = deleteLevel
            });
        }

        /// <summary>Returns all registered seed permission requests.</summary>
        public static IReadOnlyList<PermissionRequest> GetSeedPermissions() => _seedPermissions;
        /// <summary>Returns all registered invariant permission requests.</summary>
        public static IReadOnlyList<PermissionRequest> GetInvariantPermissions() => _invariantPermissions;
    }

    /// <summary>
    /// Describes a permission to be created as a seed or invariant record during startup.
    /// Specifies the role, target model, and CRUD permission levels.
    /// </summary>
    public class PermissionRequest
    {
        /// <summary>Role name to assign the permission to (e.g., "Basic Rights").</summary>
        public string RoleName { get; set; } = string.Empty;
        /// <summary>Module containing the target model.</summary>
        public string ModuleName { get; set; } = string.Empty;
        /// <summary>Model type name the permission applies to.</summary>
        public string ModelName { get; set; } = string.Empty;
        /// <summary>Whether the role can access generic UI pages for this model.</summary>
        public bool UsePages { get; set; }
        /// <summary>Whether the role can create records of this model type.</summary>
        public bool CanCreate { get; set; }
        /// <summary>Read access level (None, Own, Group, All).</summary>
        public PermissionLevel ReadLevel { get; set; }
        /// <summary>Update access level (None, Own, Group, All).</summary>
        public PermissionLevel UpdateLevel { get; set; }
        /// <summary>Delete access level (None, Own, Group, All).</summary>
        public PermissionLevel DeleteLevel { get; set; }
    }

    /// <summary>
    /// Represents a module registered with the framework, including its connection string,
    /// invariant records, seed records, and configuration entries.
    /// </summary>
    public class RegisteredModule
    {
        /// <summary>Module name (e.g., "Governance").</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Database connection string for this module.</summary>
        public string? ConnectionString { get; set; }
        /// <summary>Optional human-readable description.</summary>
        public string? Description { get; set; }
        /// <summary>Timestamp when the module was registered.</summary>
        public DateTime RegisteredAt { get; set; }        
        /// <summary>
        /// Invariant records added via AddInvariant<T>(record) during module registration.
        /// These are upserted on every startup.
        /// </summary>
        public List<object> ManualInvariants { get; set; } = new();
        
        /// <summary>
        /// Invariant config items added via AddInvariantConfig(key, value) during module registration.
        /// These are upserted on every startup.
        /// </summary>
        public Dictionary<string, string> ManualInvariantConfigs { get; set; } = new();
        
        /// <summary>
        /// Seed records added via AddSeed<T>(record) during module registration.
        /// These are inserted once at table creation.
        /// </summary>
        public List<object> ManualSeeds { get; set; } = new();
        
        /// <summary>
        /// Seed config items added via AddSeedConfig(key, value) during module registration.
        /// These are inserted once at table creation.
        /// </summary>
        public Dictionary<string, string> ManualSeedConfigs { get; set; } = new();    }
}
