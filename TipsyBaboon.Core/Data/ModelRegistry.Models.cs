using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Extensions;
using TipsyBaboon.Core.Models;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.Core.Data
{
    /// <summary>
    /// Static registry for model type discovery, schema definition caching, and metadata management.
    /// Scans assemblies for TipsyBaboon models and caches their SchemaDefinitions for use by UI and SqlServer providers.
    /// This is the authoritative source for model metadata in the framework.
    /// </summary>
    public static partial class ModelRegistry
    {
        private static readonly ConcurrentDictionary<string, DiscoveryCache> _cache = new();
        private static readonly object _cacheLock = new();

        /// <summary>
        /// Retrieves a SchemaDefinition by model type name, plural name, fully qualified name, or table name.
        /// Optionally filters by module name.
        /// </summary>
        /// <param name="typeName">Model type name (e.g., "User"), plural name, fully qualified name, or table name.</param>
        /// <param name="moduleName">Optional module name to filter results.</param>
        /// <returns>SchemaDefinition if found, otherwise null.</returns>
        public static SchemaDefinition? GetModel(string typeName, string? moduleName = null)
        {

            var match = _cache.Values
                .SelectMany(cache => cache.SchemaDefinitions.Values)
                .FirstOrDefault(sd =>
                    (moduleName == null || sd.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    &&
                    (sd.TypeName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                     sd.PluralName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                     sd.FullyQualifiedTypeName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                     sd.TableName.Equals(typeName, StringComparison.OrdinalIgnoreCase)));

            return match != null ? match.Clone() : null;
        }

        /// <summary>
        /// Retrieves a SchemaDefinition by its RecordModelId (deterministic Guid).
        /// </summary>
        /// <param name="id">The Guid identifying the model record.</param>
        /// <returns>SchemaDefinition if found, otherwise null.</returns>
        public static SchemaDefinition? GetModel(Guid id)
        {

            var match = _cache.Values
                .SelectMany(cache => cache.SchemaDefinitions.Values)
                .FirstOrDefault(sd => sd.RecordModelId == id);

            return match != null ? match.Clone() : null;
        }

        /// <summary>
        /// Retrieves a SchemaDefinition by Type. This is the preferred method for Type -> Schema lookups
        /// as it automatically resolves both module name and type name together.
        /// </summary>
        /// <param name="entityType">The model Type.</param>
        /// <returns>SchemaDefinition if found, otherwise null.</returns>
        public static SchemaDefinition? GetModel(Type entityType)
        {
            var moduleName = GetModuleName(entityType);
            return GetModel(entityType.Name, moduleName);
        }


        /// <summary>
        /// Gets the module name for a model type by reading UIEntityAttribute or ModuleNameAttribute.
        /// </summary>
        /// <param name="type">The model Type.</param>
        /// <returns>Module name string.</returns>
        /// <exception cref="Exception">Thrown if neither UIEntityAttribute nor ModuleNameAttribute is present.</exception>
        public static string GetModuleName(Type type)
        {
            var uiEntityAttr = type.GetCustomAttribute<UIEntityAttribute>();
            if (uiEntityAttr != null)
                return uiEntityAttr.ModuleName;

            var moduleNameAttr = type.GetCustomAttribute<ModuleNameAttribute>();
            if (moduleNameAttr == null)
                throw new Exception("ModuleNameAttribute is required on all managed entity types.");

            return moduleNameAttr.Name;
        }


        /// <summary>
        /// Generates a deterministic Guid for a string identifier (used for invariant records).
        /// Useful for creating stable IDs like "Role:UserAdmin" or "Permission:Governance:User:Read".
        /// </summary>
        /// <param name="deterministicName">The input string to hash.</param>
        /// <returns>Deterministic Guid derived from the input string.</returns>
        public static Guid GenerateDeterministicId(string deterministicName)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(deterministicName));
            return new Guid(hash);
        }

        #region Internal Helpers

        internal static IReadOnlyList<SchemaDefinition> GetSchemaDefinitionsForSync()
            => FilterDefinitions(d => !d.IsViewModel && !d.IsConfiguration && !d.IsUserPreference);

        internal static IReadOnlyList<SchemaDefinition> GetModelsForMenu()
            => FilterDefinitions(d => d.ShowInMenu);

        internal static IReadOnlyList<Type> DiscoverModelTypes(IEnumerable<Assembly> assemblies)
        {
            var cache = GetOrCreateCache(assemblies);
            return cache.ModelTypes.AsReadOnly();
        }

        internal static IReadOnlyList<Type> GetViewModelTypes()
        {
            var registeredModules = ModelRegistry.GetRegisteredModuleNames();
            var types = new List<Type>();
            var seenTypes = new HashSet<Type>();

            foreach (var cache in _cache.Values)
            {
                foreach (var def in cache.SchemaDefinitions.Values)
                {
                    if (def.IsViewModel && registeredModules.Contains(def.ModuleName) && seenTypes.Add(def.ModelType))
                        types.Add(def.ModelType);
                }
            }

            return types.AsReadOnly();
        }

        #endregion

        #region Private Implementation
        private static IReadOnlyList<SchemaDefinition> FilterDefinitions(Func<SchemaDefinition, bool> predicate)
        {
            var registeredModules = ModelRegistry.GetRegisteredModuleNames();
            var seenTypes = new HashSet<Type>();
            var results = new List<SchemaDefinition>();

            foreach (var cache in _cache.Values)
            {
                foreach (var def in cache.SchemaDefinitions.Values)
                {
                    if (predicate(def) && registeredModules.Contains(def.ModuleName) && seenTypes.Add(def.ModelType))
                        results.Add(def.Clone());
                }
            }

            return results.AsReadOnly();
        }


        private static DiscoveryCache GetOrCreateCache(IEnumerable<Assembly> assemblies)
        {
            var assemblyList = assemblies.ToList();
            var cacheKey = string.Join("|", assemblyList.Select(a => a.FullName).OrderBy(n => n));

            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                return existing;
            }

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out existing))
                {
                    return existing;
                }

                var cache = new DiscoveryCache();
                foreach (var asm in assemblies)
                {
                    try
                    {
                        foreach (var type in asm.GetTypes())
                        {
                            if (type.IsClass && !type.IsAbstract && ((typeof(TipsyBaboonModel).IsAssignableFrom(type) && type != typeof(TipsyBaboonModel)) || type.GetCustomAttribute<ViewModelAttribute>() != null || type.GetCustomAttribute<ConfigurationClassAttribute>() != null || type.GetCustomAttribute<UserPreferenceClassAttribute>() != null))
                            {
                                cache.ModelTypes.Add(type);
                                cache.SchemaDefinitions[type] = BuildSchemaDefinition(type);
                                #region Build ModelRecord for non-configuration types
                                if (type.GetCustomAttribute<ConfigurationClassAttribute>() == null && type.GetCustomAttribute<UserPreferenceClassAttribute>() == null)
                                {
                                    var schema = GetModel(type);
                                    cache.ModelRecords[type] = new ModelRecord
                                    {
                                        Id = GenerateDeterministicId(GetModuleName(type) + '|' + type.Name),
                                        Name = type.Name,
                                        AssemblyName = type.Assembly.GetName().Name ?? string.Empty,
                                        FullyQualifiedTypeName = type.FullName,
                                        VersionHash = ComputeTypeHash(type),
                                        TableName = type.Name,
                                        IsInMenu = schema?.ShowInMenu ?? false,
                                        HasForms = schema?.HasForms ?? false,
                                        HasLocalPermissions = schema?.HasLocalPermissions ?? false,
                                        CreatedByDisplayName = "System"
                                    };
                                }
                                #endregion
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        foreach (var type in ex.Types.Where(t => t != null))
                        {
                            if (type!.IsClass && !type.IsAbstract && ((typeof(TipsyBaboonModel).IsAssignableFrom(type) && type != typeof(TipsyBaboonModel)) || type!.GetCustomAttribute<ViewModelAttribute>() != null || type!.GetCustomAttribute<ConfigurationClassAttribute>() != null || type!.GetCustomAttribute<UserPreferenceClassAttribute>() != null))
                            {
                                cache.ModelTypes.Add(type);
                                cache.SchemaDefinitions[type] = BuildSchemaDefinition(type);
                                #region Build ModelRecord for non-configuration types
                                if (type.GetCustomAttribute<ConfigurationClassAttribute>() == null && type.GetCustomAttribute<UserPreferenceClassAttribute>() == null)
                                {
                                    var schema = GetModel(type);
                                    cache.ModelRecords[type] = new ModelRecord
                                    {
                                        Id = GenerateDeterministicId(GetModuleName(type) + '|' + type.Name),
                                        Name = type.Name,
                                        AssemblyName = type.Assembly.GetName().Name ?? string.Empty,
                                        FullyQualifiedTypeName = type.FullName,
                                        VersionHash = ComputeTypeHash(type),
                                        TableName = type.Name,
                                        IsInMenu = schema?.ShowInMenu ?? false,
                                        HasForms = schema?.HasForms ?? false,
                                        HasLocalPermissions = schema?.HasLocalPermissions ?? false,
                                        CreatedByDisplayName = "System"
                                    };
                                }
                                #endregion
                            }
                        }
                    }
                }

                #region Resolve AuthorizeAs references to link models to their authorization parents
                foreach (var schema in cache.SchemaDefinitions.Values)
                {
                    if (string.IsNullOrEmpty(schema.AuthorizeAsModelName))
                        continue;

                    var parentSchema = cache.SchemaDefinitions.Values.FirstOrDefault(s =>
                        s.TypeName.Equals(schema.AuthorizeAsModelName, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(schema.AuthorizeAsModuleName) ||
                         s.ModuleName.Equals(schema.AuthorizeAsModuleName, StringComparison.OrdinalIgnoreCase)));

                    if (parentSchema != null)
                    {
                        schema.AuthorizeAsRecordModelId = parentSchema.RecordModelId;
                    }
                }
                #endregion

                #region Collect privilege invariants from PrivilegeAttributes into Privilege schema
                var privilegeSchema = cache.SchemaDefinitions.Values.FirstOrDefault(s => s.TypeName == "Privilege");
                if (privilegeSchema != null)
                {
                    var discoveredPrivileges = new List<object>();
                    foreach (var kvp in cache.SchemaDefinitions)
                    {
                        var type = kvp.Key;
                        var schema = kvp.Value;
                        var privilegeAttrs = type.GetCustomAttributes<PrivilegeAttribute>();
                        foreach (var attr in privilegeAttrs)
                        {
                            var privilegeId = GeneratePrivilegeId(schema.RecordModelId, attr.Name);
                            var privilege = new Privilege
                            {
                                Id = privilegeId,
                                RecordId = schema.RecordModelId,
                                Name = attr.Name,
                                CreatedByDisplayName = "System",
                                CreatedOn = DateTime.UtcNow,
                                ModifiedByDisplayName = "System",
                                ModifiedOn = DateTime.UtcNow,
                                OwnershipLevel = OwnershipLevel.Public,
                                IsInvariant = true
                            };
                            discoveredPrivileges.Add(privilege);
                        }
                    }
                    privilegeSchema.InvariantRecords.AddRange(discoveredPrivileges);
                }
                #endregion

                _cache[cacheKey] = cache;

                return cache;
            }
        }

        internal static Guid GeneratePrivilegeId(Guid recordId, string privilegeName)
        {
            var combined = $"{recordId:N}|{privilegeName}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return new Guid(hash);
        }

        private static SchemaDefinition BuildSchemaDefinition(Type entityType)
        {
            // Phase 1: Gather simple metadata
            #region Extract entity display metadata from UIEntityAttribute or DisplayAttribute
            var entityMeta = (Name: entityType.Name, Description: (string?)null, Icon: (string?)null, GroupName: (string?)null, Order: 10000, ShowInMenu: false, PluralName: entityType.Name + "s", HasOwnership: true, ScriptPaths: Array.Empty<string>(), StylePaths: Array.Empty<string>(), DefaultSort: Array.Empty<string>());
            var uiEntity = entityType.GetCustomAttribute<UIEntityAttribute>();
            if (uiEntity != null)
            {
                entityMeta = (
                    uiEntity.Name,
                    uiEntity.Description,
                    uiEntity.Icon,
                    uiEntity.GroupName,
                    uiEntity.Order,
                    uiEntity.ShowInMenu,
                    uiEntity.PluralName ?? uiEntity.Name + "s",
                    uiEntity.HasOwnership,
                    uiEntity.ScriptPaths,
                    uiEntity.StylePaths,
                    uiEntity.DefaultSort
                );
            }
            else
            {
                var display = entityType.GetCustomAttribute<DisplayAttribute>();
                if (display != null)
                {
                    entityMeta = (
                        display.Name ?? entityType.Name,
                        display.Description,
                        null,
                        display.GroupName,
                        display.GetOrder() ?? 10000,
                        display.GetAutoGenerateField() ?? false,
                        entityType.Name + "s",
                        true,
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        Array.Empty<string>()
                    );
                }
            }
            #endregion
            var allowedOwnershipLevels = entityType.GetCustomAttribute<OwnershipLevelsAttribute>()?.AllowedLevels.ToList()
                ?? new List<OwnershipLevel> { OwnershipLevel.Public };
            var authorizeAsAttr = entityType.GetCustomAttribute<AuthorizeAsAttribute>();
            var hasUIEntity = entityType.GetCustomAttribute<UIEntityAttribute>() != null;

            // Phase 2: Create the root SchemaDefinition
            var isConfiguration = entityType.GetCustomAttribute<ConfigurationClassAttribute>() != null;
            var isUserPreference = entityType.GetCustomAttribute<UserPreferenceClassAttribute>() != null;
            var moduleName = GetModuleName(entityType);
            var typeName = entityType.Name;
            var definition = new SchemaDefinition
            {
                ModelType = entityType,
                RecordModelId = GenerateDeterministicId(moduleName + '|' + typeName),
                TypeName = entityType.Name,
                FullyQualifiedTypeName = entityType.FullName ?? entityType.Name,
                AssemblyName = entityType.Assembly.GetName().Name ?? "",
                TableName = entityType.GetCustomAttribute<TableAttribute>()?.Name ?? entityType.Name,
                ModuleName = moduleName,
                VersionHash = ComputeTypeHash(entityType),
                IsViewModel = entityType.GetCustomAttribute<ViewModelAttribute>() != null,
                IsConfiguration = isConfiguration,
                IsUserPreference = isUserPreference,
                OwnershipLevels = allowedOwnershipLevels,
                HasOwnership = entityMeta.HasOwnership,
                AuthorizeAsModelName = authorizeAsAttr?.IsNone == true ? null : authorizeAsAttr?.ParentModelName,
                AuthorizeAsModuleName = authorizeAsAttr?.IsNone == true ? null : authorizeAsAttr?.ParentModuleName,
                SkipPermissionChecks = authorizeAsAttr?.IsNone == true,
                HasForms = hasUIEntity,
                IsReadOnly = entityType.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? false,
                IsUIReadOnly = entityType.GetCustomAttribute<UIReadOnlyAttribute>() != null || (entityType.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? false),
                ShowInMenu = entityMeta.ShowInMenu,
                FullScreenMode = entityType.GetCustomAttribute<FullScreenOnlyAttribute>()?.Mode ?? FullScreenMode.None,
                MenuLabel = entityMeta.Name,
                MenuIcon = entityMeta.Icon,
                MenuOrder = entityMeta.Order,
                PluralName = entityMeta.PluralName,
                Description = entityMeta.Description,
                GroupName = entityMeta.GroupName,
                ScriptPaths = entityMeta.ScriptPaths,
                StylePaths = entityMeta.StylePaths,
                DefaultSort = entityMeta.DefaultSort
            };

            // Phase 3: Build columns from all properties (regular and navigation)
            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null || p.GetCustomAttribute<UIDisplayAttribute>() != null);

            definition.Columns = properties.Select(p =>
            {
                #region Extract property display metadata (name, description, prompt, group, order)
                var displayMeta = (Name: p.Name, Description: (string?)null, Prompt: (string?)null, GroupName: "Details", Order: 10000);
                var uiDisplay = p.GetCustomAttribute<UIDisplayAttribute>();
                if (uiDisplay != null)
                {
                    displayMeta = (
                        uiDisplay.Name ?? p.Name,
                        uiDisplay.Description,
                        uiDisplay.Prompt,
                        uiDisplay.GroupName,
                        uiDisplay.Order
                    );
                }
                else
                {
                    var display = p.GetCustomAttribute<DisplayAttribute>();
                    if (display != null)
                    {
                        displayMeta = (
                            display.Name ?? p.Name,
                            display.Description,
                            display.Prompt,
                            display.GroupName,
                            display.GetOrder() ?? 10000
                        );
                    }
                }
                #endregion
                
                #region Extract property visibility metadata (ShowInList, ShowInDetail, ColumnSize)
                var visibilityMeta = (ShowInList: false, ShowInDetail: false, ColumnSize: 12);
                if (uiDisplay != null)
                {
                    visibilityMeta = (uiDisplay.ShowInList, uiDisplay.ShowInDetail, uiDisplay.ColumnSize);
                }
                else
                {
                    var display = p.GetCustomAttribute<DisplayAttribute>();
                    if (display != null)
                    {
                        var autoDisplay = display.GetAutoGenerateField() ?? true;
                        visibilityMeta = (autoDisplay, autoDisplay, 12);
                    }
                }
                #endregion
                var fkAttr = p.GetCustomAttribute<ForeignKeyAttribute>();
                
                #region Check if navigation property
                var propType = p.PropertyType;
                var isNavigationProperty = false;
                if (propType.IsGenericType)
                {
                    var genericDef = propType.GetGenericTypeDefinition();
                    if (genericDef == typeof(List<>) ||
                        genericDef == typeof(ICollection<>) ||
                        genericDef == typeof(IEnumerable<>) ||
                        genericDef == typeof(IList<>))
                    {
                        isNavigationProperty = true;
                    }
                }
                if (typeof(TipsyBaboonModel).IsAssignableFrom(propType))
                    isNavigationProperty = true;
                #endregion

                // For navigation properties, get the element type
                Type? navigationElementType = null;
                string? navigationElementTypeName = null;
                bool isNavigationViewModel = false;
                string? navigationForeignKeyProperty = null;

                if (isNavigationProperty)
                {
                    // Get collection element type
                    if (p.PropertyType.IsGenericType)
                    {
                        var genericDef = p.PropertyType.GetGenericTypeDefinition();
                        if (genericDef == typeof(List<>) ||
                            genericDef == typeof(ICollection<>) ||
                            genericDef == typeof(IEnumerable<>) ||
                            genericDef == typeof(IList<>))
                        {
                            navigationElementType = p.PropertyType.GetGenericArguments().FirstOrDefault();
                        }
                    }
                    
                    if (navigationElementType == null || !typeof(TipsyBaboonModel).IsAssignableFrom(navigationElementType))
                        return null; // Skip invalid navigation properties

                    var includeAttr = p.GetCustomAttribute<IncludeNavigationAttribute>();
                    var hasDisplayAttr = uiDisplay != null || p.GetCustomAttribute<DisplayAttribute>() != null;
                    
                    if (!hasDisplayAttr && includeAttr == null)
                        return null; // Skip navigation properties without display attributes

                    navigationElementTypeName = navigationElementType.Name;
                    isNavigationViewModel = navigationElementType.GetCustomAttribute<ViewModelAttribute>() != null;
                    
                    // Determine navigation foreign key
                    if (fkAttr != null)
                    {
                        navigationForeignKeyProperty = fkAttr.Name;
                    }
                    else
                    {
                        var propsWithFkAttr = navigationElementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(fk => fk.GetCustomAttribute<ForeignKeyAttribute>()?.Name == entityType.Name)
                            .ToList();
                        
                        if (propsWithFkAttr.Count == 1)
                            navigationForeignKeyProperty = propsWithFkAttr[0].Name;
                        else
                        {
                            var expectedFkName = $"{entityType.Name}Id";
                            var fkProp = navigationElementType.GetProperty(expectedFkName, BindingFlags.Public | BindingFlags.Instance);
                            if (fkProp != null)
                                navigationForeignKeyProperty = expectedFkName;
                        }
                    }
                }

                #region Compute maxLength from MaxLengthAttribute or StringLengthAttribute
                var maxLen = -1;
                if (!isNavigationProperty)
                {
                    var maxLengthAttr = p.GetCustomAttribute<MaxLengthAttribute>();
                    if (maxLengthAttr != null)
                        maxLen = maxLengthAttr.Length;
                    else
                    {
                        var stringLengthAttr = p.GetCustomAttribute<StringLengthAttribute>();
                        if (stringLengthAttr != null)
                            maxLen = stringLengthAttr.MaximumLength;
                    }
                }
                #endregion

                #region Determine control name from FieldControlAttribute or DataTypeAttribute
                string? controlName = null;
                var fieldControlAttr = p.GetCustomAttribute<FieldControlAttribute>();
                if (fieldControlAttr != null)
                {
                    controlName = fieldControlAttr.Name;
                }
                else
                {
                    var dataTypeAttr = p.GetCustomAttribute<DataTypeAttribute>();
                    if (dataTypeAttr != null)
                    {
                        controlName = dataTypeAttr.DataType switch
                        {
                            DataType.Password => "password",
                            DataType.EmailAddress => "email",
                            DataType.PhoneNumber => "tel",
                            DataType.Url => "url",
                            DataType.MultilineText => "textarea",
                            DataType.Date => "date",
                            DataType.Time => "time",
                            DataType.DateTime => "datetime-local",
                            _ => null
                        };
                    }
                }
                #endregion

                #region Parse control config from FieldControlAttribute
                Dictionary<string, string>? controlConfig = null;
                if (!string.IsNullOrWhiteSpace(fieldControlAttr?.ConfigJson))
                {
                    try
                    {
                        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        using var doc = System.Text.Json.JsonDocument.Parse(fieldControlAttr.ConfigJson);
                        
                        foreach (var property in doc.RootElement.EnumerateObject())
                        {
                            string value;
                            
                            // Handle different JSON value types
                            switch (property.Value.ValueKind)
                            {
                                case System.Text.Json.JsonValueKind.Array:
                                    // Convert array to comma-separated string
                                    var items = property.Value.EnumerateArray()
                                        .Select(e => e.GetString() ?? "")
                                        .Where(s => !string.IsNullOrEmpty(s));
                                    value = string.Join(",", items);
                                    break;
                                    
                                case System.Text.Json.JsonValueKind.String:
                                    value = property.Value.GetString() ?? "";
                                    break;
                                    
                                case System.Text.Json.JsonValueKind.Number:
                                case System.Text.Json.JsonValueKind.True:
                                case System.Text.Json.JsonValueKind.False:
                                    value = property.Value.ToString();
                                    break;
                                    
                                default:
                                    value = property.Value.GetRawText();
                                    break;
                            }
                            
                            if (!string.IsNullOrEmpty(value))
                                config[property.Name] = value;
                        }
                        
                        if (config.Count > 0)
                            controlConfig = config;
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        throw new InvalidOperationException(
                            $"Invalid JSON in FieldControlAttribute.ConfigJson for property '{p.Name}' on type '{entityType.Name}': {ex.Message}", ex);
                    }
                }
                #endregion

                var col = new ColumnDefinition
                {
                    PropertyName = p.Name,
                    ColumnName = p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name,
                    ClrType = isNavigationProperty ? typeof(ICollection<>).MakeGenericType(navigationElementType!) : p.PropertyType,
                    ClrTypeName = isNavigationProperty ? $"ICollection<{navigationElementTypeName}>" : ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).FullName ?? (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).Name),
                    MaxLength = maxLen,
                    IsNullable = isNavigationProperty ? true : (p.PropertyType.IsValueType ? Nullable.GetUnderlyingType(p.PropertyType) != null : p.GetCustomAttribute<RequiredAttribute>() == null),
                    IsPrimaryKey = isNavigationProperty ? false : (p.GetCustomAttribute<KeyAttribute>() != null || (entityType.GetCustomAttribute<PrimaryKeyAttribute>()?.PropertyNames.Contains(p.Name) ?? false)),
                    KeyOrdinal = isNavigationProperty ? (int?)null : (p.GetCustomAttribute<KeyAttribute>() != null ? (p.GetCustomAttribute<ColumnAttribute>()?.Order ?? 0) : (entityType.GetCustomAttribute<PrimaryKeyAttribute>() != null ? Array.IndexOf(entityType.GetCustomAttribute<PrimaryKeyAttribute>()!.PropertyNames, p.Name) : -1) >= 0 ? Array.IndexOf(entityType.GetCustomAttribute<PrimaryKeyAttribute>()!.PropertyNames, p.Name) : (int?)null),
                    Precision = isNavigationProperty ? 18 : ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(decimal) ? 18 : 0),
                    Scale = isNavigationProperty ? 2 : ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(decimal) ? 2 : 0),
                    IsUnicode = true,
                    DatabaseGenerated = isNavigationProperty ? null : p.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption,
                    IsNotMapped = p.GetCustomAttribute<NotMappedAttribute>() != null,
                    IsRecordName = isNavigationProperty ? false : p.GetCustomAttribute<RecordNameAttribute>() != null,

                    IsForeignKey = !isNavigationProperty && fkAttr != null,
                    ForeignKeyTarget = !isNavigationProperty ? fkAttr?.Name : null,
                    CascadeOnDelete = !isNavigationProperty && p.GetCustomAttribute<CascadeDeleteAttribute>() != null,

                    DisplayName = displayMeta.Name,
                    Description = displayMeta.Description ?? "",
                    HasUIDisplay = uiDisplay != null,
                    ShowInList = visibilityMeta.ShowInList,
                    ShowInForm = visibilityMeta.ShowInDetail,
                    ShowLabel = uiDisplay?.ShowLabel ?? true,
                    ColumnSize = uiDisplay?.ColumnSize ?? 12,
                    IsRequired = !isNavigationProperty && p.GetCustomAttribute<RequiredAttribute>() != null,
                    IsReadOnly = isNavigationProperty ? false : (definition.IsReadOnly || p.GetCustomAttribute<ReadOnlyAttribute>() != null),
                    IsUIReadOnly = isNavigationProperty ? false : (definition.IsUIReadOnly || p.GetCustomAttribute<UIReadOnlyAttribute>() != null || p.GetCustomAttribute<ReadOnlyAttribute>() != null),
                    RequiredPrivilege = isNavigationProperty ? null : p.GetCustomAttribute<RequirePrivilegeAttribute>()?.PrivilegeName,
                    DisplayOrder = displayMeta.Order,
                    GroupName = displayMeta.GroupName ?? "Details",
                    ControlName = controlName,
                    ControlConfig = controlConfig,

                    IsNavigationProperty = isNavigationProperty,
                    NavigationElementType = navigationElementType,
                    NavigationElementTypeName = navigationElementTypeName,
                    IsNavigationViewModel = isNavigationViewModel,
                    NavigationInDetail = p.GetCustomAttribute<IncludeNavigationAttribute>()?.InDetail ?? false,
                    NavigationInGrid = p.GetCustomAttribute<IncludeNavigationAttribute>()?.InGrid ?? false,
                    NavigationForeignKeyProperty = navigationForeignKeyProperty
                };

                #region Apply validation metadata from ValidationAttributes
                if (!isNavigationProperty)
                {
                    var validationAttrs = p.GetCustomAttributes<ValidationAttribute>().ToList();
                    if (validationAttrs.Any())
                    {
                        foreach (var attr in validationAttrs)
                        {
                            switch (attr)
                            {
                                case RequiredAttribute required:
                                    col.IsRequired = true;
                                    col.RequiredErrorMessage = required.ErrorMessage;
                                    break;

                                case StringLengthAttribute stringLength:
                                    if (stringLength.MaximumLength > 0 && (col.MaxLength < 0 || stringLength.MaximumLength < col.MaxLength))
                                        col.MaxLength = stringLength.MaximumLength;
                                    if (stringLength.MinimumLength > 0)
                                        col.MinLength = stringLength.MinimumLength;
                                    col.LengthErrorMessage = stringLength.ErrorMessage;
                                    break;

                                case MinLengthAttribute minLength:
                                    col.MinLength = minLength.Length;
                                    col.LengthErrorMessage ??= minLength.ErrorMessage;
                                    break;

                                case MaxLengthAttribute maxLength:
                                    if (col.MaxLength < 0 || maxLength.Length < col.MaxLength)
                                        col.MaxLength = maxLength.Length;
                                    col.LengthErrorMessage ??= maxLength.ErrorMessage;
                                    break;

                                case RangeAttribute range:
                                    col.RangeMin = range.Minimum;
                                    col.RangeMax = range.Maximum;
                                    col.RangeErrorMessage = range.ErrorMessage;
                                    break;

                                case RegularExpressionAttribute regex:
                                    col.Pattern = regex.Pattern;
                                    col.PatternErrorMessage = regex.ErrorMessage;
                                    break;

                                case CompareAttribute compare:
                                    col.CompareProperty = compare.OtherProperty;
                                    col.CompareErrorMessage = compare.ErrorMessage;
                                    break;

                                case EmailAddressAttribute email:
                                    col.Pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                                    col.FormatErrorMessage = email.ErrorMessage;
                                    break;

                                case UrlAttribute url:
                                    col.Pattern = @"^(https?://)?([a-z0-9-]+\.)+[a-z]{2,6}(/[^ ]*)?$";
                                    col.FormatErrorMessage = url.ErrorMessage;
                                    break;

                                case PhoneAttribute phone:
                                    col.Pattern = @"^\+?[1-9]\d{1,14}$";
                                    col.FormatErrorMessage = phone.ErrorMessage;
                                    break;

                                case CreditCardAttribute creditCard:
                                    col.Pattern = @"^\d{4}-\d{4}-\d{4}-\d{4}$";
                                    col.FormatErrorMessage = creditCard.ErrorMessage;
                                    break;
                            }
                        }
                    }
                }
                #endregion

                return col;
            }).Where(c => c != null).Cast<ColumnDefinition>().ToList();

            definition.RecordNameProperty = definition.Columns.FirstOrDefault(c => c.IsRecordName)?.PropertyName;

            // Phase 4: Validate view models
            if (entityType.GetCustomAttribute<ViewModelAttribute>() != null)
            {
                var prop = entityType.GetProperty("ViewDefinition", BindingFlags.Public | BindingFlags.Static);
                if (prop == null || !typeof(ViewDefinition).IsAssignableFrom(prop.PropertyType))
                {
                    throw new InvalidOperationException($"View model {entityType.FullName} must declare a public static property 'ViewDefinition' of type ViewDefinition.");
                }
            }

            // Phase 5: Build layout sections from columns
            var uiGroups = entityType.GetCustomAttributes<UIGroupAttribute>().ToDictionary(g => g.Name, g => g);
            var defaultLayout = new List<LayoutSection>();
            var formColumns = definition.Columns.Where(c => c.ShowInForm  && c.DisplayOrder >= 0);
            
            if (formColumns.Any())
            {
                // If no UIGroups defined, ensure "Details" group exists
                if (!uiGroups.Any())
                {
                    uiGroups["Details"] = new UIGroupAttribute("Details")
                    {
                        Title = "Details",
                        Order = 100
                    };
                }
                
                // Discover any missing groups referenced by properties and create with intuitive defaults
                var discoveredGroupOrder = 1000; // Start well after typical explicit orders
                foreach (var group in formColumns.GroupBy(c => c.GroupName ?? "Details"))
                {
                    if (!uiGroups.ContainsKey(group.Key))
                    {
                        // Auto-create missing group based on property references
                        var firstProperty = group.OrderBy(c => c.DisplayOrder).First();
                        uiGroups[group.Key] = new UIGroupAttribute(group.Key)
                        {
                            Title = group.Key,
                            Order = discoveredGroupOrder++
                        };
                    }
                }
                
                foreach (var group in formColumns.GroupBy(c => c.GroupName ?? "Details").OrderBy(g => 
                    uiGroups.TryGetValue(g.Key, out var uiGroup) ? uiGroup.Order : 100))
                {
                    var uiGroup = uiGroups[group.Key]; // Now guaranteed to exist
                    
                    defaultLayout.Add(new LayoutSection
                    {
                        Title = uiGroup.Title,
                        Description = uiGroup.Description,
                        Order = uiGroup.Order,
                        IsCollapsible = uiGroup.Collapsible,
                        IsInitiallyCollapsed = uiGroup.InitiallyCollapsed,
                        ColumnSize = 12,
                        CssClass = uiGroup.CssClass,
                        Properties = group
                            .OrderBy(c => c.DisplayOrder ?? int.MaxValue)
                            .ThenBy(c => c.PropertyName)
                            .Select(c => new LayoutProperty
                            {
                                PropertyName = c.PropertyName,
                                Order = c.DisplayOrder ?? int.MaxValue,
                                ColumnSize = c.ColumnSize,
                                ShowLabel = c.ShowLabel
                            })
                            .ToList()
                    });
                }
            }
            else if (!uiGroups.Any())
            {
                // No form columns but ensure "Details" group exists in schema for consistency
                defaultLayout.Add(new LayoutSection
                {
                    Title = "Details",
                    Order = 100,
                    ColumnSize = 12,
                    Properties = new List<LayoutProperty>()
                });
            }
            definition.Layout = defaultLayout;

            // Phase 6: Discover and apply indexes (only on non-navigation properties)
            #region Discover indexes from class-level and property-level IndexAttributes
            var indexableProps = definition.Columns
                .Where(c => !c.IsNavigationProperty)
                .Select(c => entityType.GetProperty(c.PropertyName)!)
                .Where(p => p != null)
                .ToList();
            
            var indexes = new Dictionary<string, IndexDefinition>();
            
            // 1. Discover class-level indexes (both TipsyBaboon and EF)
            var classIndexResults = new List<(dynamic, string[]?)>();
            var tipsyIndexes = entityType.GetCustomAttributes(typeof(Attributes.IndexAttribute), false);
            foreach (var attr in tipsyIndexes)
            {
                var indexAttr = (Attributes.IndexAttribute)attr;
                classIndexResults.Add((new { indexAttr.Name, indexAttr.IsUnique, indexAttr.IsClustered, Order = 0 }, indexAttr.PropertyNames));
            }
            try
            {
                var efIndexType = Type.GetType("Microsoft.EntityFrameworkCore.IndexAttribute, Microsoft.EntityFrameworkCore", false);
                if (efIndexType != null)
                {
                    var efIndexes = entityType.GetCustomAttributes(efIndexType, false);
                    foreach (var attr in efIndexes)
                    {
                        var nameProperty = efIndexType.GetProperty("Name");
                        var isUniqueProperty = efIndexType.GetProperty("IsUnique");
                        var isClusteredProperty = efIndexType.GetProperty("IsClustered");
                        var propertyNamesProperty = efIndexType.GetProperty("PropertyNames");
                        var name = nameProperty?.GetValue(attr) as string;
                        var isUnique = isUniqueProperty?.GetValue(attr) as bool? ?? false;
                        var isClustered = isClusteredProperty?.GetValue(attr) as bool? ?? false;
                        var propertyNames = propertyNamesProperty?.GetValue(attr) as string[];
                        classIndexResults.Add((new { Name = name, IsUnique = isUnique, IsClustered = isClustered, Order = 0 }, propertyNames));
                    }
                }
            }
            catch { }
            
            foreach (var (indexAttr, propertyNames) in classIndexResults)
            {
                if (propertyNames != null && propertyNames.Length > 0)
                {
                    var indexName = indexAttr.Name ?? $"IX_{entityType.Name}_{string.Join("_", propertyNames)}";
                    indexes[indexName] = new IndexDefinition
                    {
                        IndexName = indexName,
                        ColumnNames = propertyNames.ToList(),
                        IsUnique = indexAttr.IsUnique,
                        IsClustered = indexAttr.IsClustered
                    };
                }
            }
            
            // 2. Discover property-level indexes (both TipsyBaboon and EF)
            var propertyIndexGroups = new Dictionary<string, List<(string PropertyName, int Order, bool IsUnique, bool IsClustered)>>();
            foreach (var prop in indexableProps)
            {
                var propIndexResults = new List<dynamic>();
                var tipsyPropIndexes = prop.GetCustomAttributes(typeof(Attributes.IndexAttribute), false);
                foreach (var attr in tipsyPropIndexes)
                {
                    var indexAttr = (Attributes.IndexAttribute)attr;
                    propIndexResults.Add(new { indexAttr.Name, indexAttr.IsUnique, indexAttr.IsClustered, indexAttr.Order });
                }
                try
                {
                    var efIndexType = Type.GetType("Microsoft.EntityFrameworkCore.IndexAttribute, Microsoft.EntityFrameworkCore", false);
                    if (efIndexType != null)
                    {
                        var efPropIndexes = prop.GetCustomAttributes(efIndexType, false);
                        foreach (var attr in efPropIndexes)
                        {
                            var nameProperty = efIndexType.GetProperty("Name");
                            var isUniqueProperty = efIndexType.GetProperty("IsUnique");
                            var isClusteredProperty = efIndexType.GetProperty("IsClustered");
                            var orderProperty = efIndexType.GetProperty("Order");
                            var name = nameProperty?.GetValue(attr) as string;
                            var isUnique = isUniqueProperty?.GetValue(attr) as bool? ?? false;
                            var isClustered = isClusteredProperty?.GetValue(attr) as bool? ?? false;
                            var order = orderProperty?.GetValue(attr) as int? ?? 0;
                            propIndexResults.Add(new { Name = name, IsUnique = isUnique, IsClustered = isClustered, Order = order });
                        }
                    }
                }
                catch { }
                
                foreach (var indexAttr in propIndexResults)
                {
                    var indexName = indexAttr.Name ?? $"IX_{entityType.Name}_{prop.Name}";
                    if (!propertyIndexGroups.ContainsKey(indexName))
                        propertyIndexGroups[indexName] = new List<(string, int, bool, bool)>();
                    propertyIndexGroups[indexName].Add((prop.Name, indexAttr.Order, indexAttr.IsUnique, indexAttr.IsClustered));
                }
            }
            
            foreach (var (indexName, props) in propertyIndexGroups)
            {
                var orderedProps = props.OrderBy(p => p.Order).ToList();
                var firstProp = orderedProps.First();
                indexes[indexName] = new IndexDefinition
                {
                    IndexName = indexName,
                    ColumnNames = orderedProps.Select(p => p.PropertyName).ToList(),
                    IsUnique = firstProp.IsUnique,
                    IsClustered = firstProp.IsClustered
                };
            }
            var indexList = indexes.Values.ToList();
            #endregion
            
            foreach (var index in indexList)
            {
                for (int i = 0; i < index.ColumnNames.Count; i++)
                {
                    var col = definition.Columns.FirstOrDefault(c => c.PropertyName == index.ColumnNames[i]);
                    if (col != null)
                    {
                        col.IndexParticipations.Add(new IndexParticipation
                        {
                            IndexName = index.IndexName,
                            Order = i,
                            IsUnique = index.IsUnique,
                            IsClustered = index.IsClustered
                        });
                    }
                }
            }
            definition.Indexes = indexList;

            // Phase 7: Discover invariant and seed records
            var invariantRecords = new List<object>();
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttribute<GenerateInvariantRecordsAttribute>() != null))
            {
                if (prop.PropertyType.IsGenericType && 
                    typeof(IEnumerable<>).IsAssignableFrom(prop.PropertyType.GetGenericTypeDefinition()))
                {
                    var value = prop.GetValue(null);
                    if (value is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item != null) invariantRecords.Add(item);
                        }
                    }
                }
            }
            definition.InvariantRecords = invariantRecords;
            
            var seedRecords = new List<object>();
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttribute<GenerateSeedRecordsAttribute>() != null))
            {
                if (prop.PropertyType.IsGenericType && 
                    typeof(IEnumerable<>).IsAssignableFrom(prop.PropertyType.GetGenericTypeDefinition()))
                {
                    var value = prop.GetValue(null);
                    if (value is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item != null) seedRecords.Add(item);
                        }
                    }
                }
            }
            definition.SeedRecords = seedRecords;
            
            // Phase 8: Discover UIAction methods
            var actions = new List<UIActionDefinition>();
            var methods = entityType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<UIActionAttribute>() != null);
            
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<UIActionAttribute>()!;
                var isStatic = method.IsStatic;
                
                actions.Add(new UIActionDefinition
                {
                    ActionId = method.Name,
                    MethodName = method.Name,
                    DisplayName = attr.DisplayName,
                    Description = attr.Description,
                    Icon = attr.Icon,
                    ButtonClass = attr.ButtonClass ?? "btn-primary",
                    RequiresSelection = !isStatic,
                    RequireConfirmation = attr.RequireConfirmation,
                    ConfirmationMessage = attr.ConfirmationMessage,
                    Order = attr.Order,
                    ModuleName = attr.ModuleName,
                    ModelName = attr.ModelName,
                    Privilege = attr.Privilege,
                    PermissionLevel = attr.PermissionLevel,
                    JSFunction = attr.JSFunction,
                    ToolbarPosition = isStatic ? "left" : null,
                    Visible = attr.Visible
                });
            }
            definition.Actions = actions;
            
            return definition;
        }

        private static string ComputeTypeHash(Type t)
        {
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(p => p.Name)
                         .Select(p =>
                         {
                             var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                             var nullable = Nullable.GetUnderlyingType(p.PropertyType) != null || !p.PropertyType.IsValueType;
                             
                             // Inline GetMaxLength logic
                             var maxLen = -1;
                             var maxLengthAttr = p.GetCustomAttribute<MaxLengthAttribute>();
                             if (maxLengthAttr != null)
                                 maxLen = maxLengthAttr.Length;
                             else
                             {
                                 var stringLengthAttr = p.GetCustomAttribute<StringLengthAttribute>();
                                 if (stringLengthAttr != null)
                                     maxLen = stringLengthAttr.MaximumLength;
                             }
                             
                             var lenPart = maxLen > 0 ? $",len:{maxLen}" : "";
                             return $"{p.Name}:{type.FullName}{(nullable ? "?" : "")}{lenPart}";
                         });

            var toHash = t.FullName + "|" + string.Join(";", props);

            var viewAttr = t.GetCustomAttribute<ViewModelAttribute>();
            if (viewAttr != null)
            {
                var prop = t.GetProperty("ViewDefinition", BindingFlags.Public | BindingFlags.Static);
                if (prop != null && typeof(ViewDefinition).IsAssignableFrom(prop.PropertyType))
                {
                    var viewDef = (ViewDefinition?)prop.GetValue(null);
                    if (viewDef != null)
                    {
                        var parts = new List<string>();
                        foreach (var table in viewDef.Tables.OrderBy(t => t.Alias))
                            parts.Add($"T:{table.Alias}:{table.TableName}:{table.JoinType}");
                        foreach (var join in viewDef.Joins.OrderBy(j => j.LeftTable).ThenBy(j => j.LeftColumn))
                            parts.Add($"J:{join.LeftTable}.{join.LeftColumn}={join.RightTable}.{join.RightColumn}");
                        foreach (var col in viewDef.Columns.OrderBy(c => c.TargetProperty))
                            parts.Add($"C:{col.TargetProperty}={col.SourceExpression}");
                        if (!string.IsNullOrWhiteSpace(viewDef.WhereClause))
                            parts.Add($"W:{viewDef.WhereClause}");
                        toHash += "|" + string.Join("|", parts);
                    }
                    else
                    {
                        toHash += "|viewdef:null";
                    }
                }
                else
                {
                    toHash += "|viewdef:missing";
                }
            }

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(toHash);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        #endregion

        private class DiscoveryCache
        {
            public List<Type> ModelTypes { get; } = new();
            public Dictionary<Type, SchemaDefinition> SchemaDefinitions { get; } = new();
            public Dictionary<Type, ModelRecord> ModelRecords { get; } = new();
        }
    }

    /// <summary>
    /// Complete schema definition for a model type.
    /// Contains table metadata, column definitions, indexes, UI layout, actions, and seed data.
    /// This is the central metadata object used by UI rendering, schema sync, and authorization.
    /// </summary>
    public class SchemaDefinition
    {
        public Type ModelType { get; set; } = null!;
        public Guid RecordModelId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string FullyQualifiedTypeName { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = "TipsyBaboon.Core";
        public List<ColumnDefinition> Columns { get; set; } = new();
        public string? VersionHash { get; set; }
        public bool IsViewModel { get; set; }
        public bool IsConfiguration { get; set; }
        public bool IsUserPreference { get; set; }
        public List<OwnershipLevel> OwnershipLevels { get; set; } = new() { OwnershipLevel.Public };
        public bool HasOwnership { get; set; } = true;
        
        public string? AuthorizeAsModelName { get; set; }
        public string? AuthorizeAsModuleName { get; set; }
        public Guid? AuthorizeAsRecordModelId { get; set; }
        public Guid EffectiveRecordModelId => AuthorizeAsRecordModelId ?? RecordModelId;
        public bool SkipPermissionChecks { get; set; } = false;
        public bool HasLocalPermissions => !SkipPermissionChecks && AuthorizeAsRecordModelId == null;

        public bool HasForms { get; set; } = false;
        public bool IsReadOnly{ get; set; } = false;
        public bool IsUIReadOnly { get; set; } = false;
        public bool ShowInMenu { get; set; } = true;
        public FullScreenMode FullScreenMode { get; set; } = FullScreenMode.None;
        public string? MenuLabel { get; set; }
        public string? MenuIcon { get; set; }
        public int MenuOrder { get; set; } = 10000;
        public string PluralName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? GroupName { get; set; }
        public string? RecordNameProperty { get; set; }
        public string[] ScriptPaths { get; set; } = [];
        public string[] StylePaths { get; set; } = [];
        public string[] DefaultSort { get; set; } = [];
        public List<LayoutSection> Layout { get; set; } = new();
        public List<UIActionDefinition> Actions { get; set; } = new();
        public List<IndexDefinition> Indexes { get; set; } = new();

        /// <summary>
        /// Invariant records discovered from [GenerateInvariantRecords] attribute.
        /// These are code-locked records (IsInvariant = true) that are upserted during schema enforcement.
        /// </summary>
        public List<object> InvariantRecords { get; set; } = new();

        /// <summary>
        /// Seed records discovered from [GenerateSeedRecords] attribute.
        /// These are inserted once at table creation and not maintained.
        /// </summary>
        public List<object> SeedRecords { get; set; } = new();

        public List<ColumnDefinition> KeyColumns => Columns
            .Where(c => c.IsPrimaryKey)
            .OrderBy(c => c.KeyOrdinal ?? 0)
            .ToList();
    }

    // Internal struct used only during navigation property discovery phase
    internal struct NavigationPropertyDiscovery
    {
        public string PropertyName { get; set; }
        public Type ElementType { get; set; }
        public string ElementTypeName { get; set; }
        public bool IsViewModel { get; set; }
        public bool InDetail { get; set; }
        public bool InGrid { get; set; }
        public string? ForeignKeyProperty { get; set; }
        
        public string DisplayName { get; set; }
        public string? Description { get; set; }
        public string? GroupName { get; set; }
        public bool ShowInList { get; set; }
        public bool ShowInForm { get; set; }
        public int? DisplayOrder { get; set; }
        public string? ControlName { get; set; }
    }

    /// <summary>
    /// Column definition for a model property.
    /// Contains database mapping, UI display metadata, validation rules, and navigation property information.
    /// </summary>
    public class ColumnDefinition
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public Type ClrType { get; set; } = typeof(object);
        public string ClrTypeName { get; set; } = string.Empty;
        public int MaxLength { get; set; } = -1;
        public bool IsNullable { get; set; } = true;
        public bool IsPrimaryKey { get; set; }
        public int? KeyOrdinal { get; set; }
        
        public bool IsForeignKey { get; set; }
        public string? ForeignKeyTarget { get; set; }
        public bool CascadeOnDelete { get; set; }
        public int Precision { get; set; } = 18;
        public int Scale { get; set; } = 2;
        public bool IsUnicode { get; set; } = true;
        public DatabaseGeneratedOption? DatabaseGenerated { get; set; }
        public bool IsNotMapped { get; set; }
        
        public List<IndexParticipation> IndexParticipations { get; set; } = new();
        
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = "";
        public string GroupName { get; set; } = "Details";
        public bool HasUIDisplay { get; set; }
        public bool ShowInList { get; set; } = true;
        public bool ShowInForm { get; set; } = true;
        public bool ShowLabel { get; set; } = true;
        public int ColumnSize { get; set; } = 12;
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsUIReadOnly { get; set; }
        public string? RequiredPrivilege { get; set; }
        public int? DisplayOrder { get; set; } = 100;
        public bool IsRecordName { get; set; }
        
        // Navigation property specific fields
        public bool IsNavigationProperty { get; set; }
        public Type? NavigationElementType { get; set; }
        public string? NavigationElementTypeName { get; set; }
        public bool IsNavigationViewModel { get; set; }
        public bool NavigationInDetail { get; set; }
        public bool NavigationInGrid { get; set; }
        public string? NavigationForeignKeyProperty { get; set; }
        
        public string? ControlName { get; set; }
        
        public Dictionary<string, string>? ControlConfig { get; set; }
        
        public int? MinLength { get; set; }
        public object? RangeMin { get; set; }
        public object? RangeMax { get; set; }
        public string? Pattern { get; set; }
        public string? CompareProperty { get; set; }
        public string? RequiredErrorMessage { get; set; }
        public string? LengthErrorMessage { get; set; }
        public string? RangeErrorMessage { get; set; }
        public string? PatternErrorMessage { get; set; }
        public string? CompareErrorMessage { get; set; }
        public string? FormatErrorMessage { get; set; }
    }

    /// <summary>
    /// UI action definition for a method marked with [UIAction] attribute.
    /// Describes toolbar buttons, grid actions, and their permission requirements.
    /// </summary>
    public class UIActionDefinition
    {
        public string ActionId { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? ButtonClass { get; set; } = "btn-primary";
        public bool RequiresSelection { get; set; }
        public bool RequireConfirmation { get; set; }
        public string? ConfirmationMessage { get; set; }
        public int Order { get; set; } = 100;
        public bool Visible { get; set; } = true;
        
        // Permission checking fields
        public string? ModuleName { get; set; }
        public string? ModelName { get; set; }
        public string? Privilege { get; set; }
        public ActionType? PermissionLevel { get; set; }
        
        // UI behavior
        public string? JSFunction { get; set; }
        public string? ToolbarPosition { get; set; } = "left";
        
        // Legacy fields for compatibility with existing GridBuilder code
        public string? Handler { get; set; }
        public string? UrlPattern { get; set; }
        public string? PermissionRequired { get; set; }
    }

    /// <summary>
    /// Database index definition with participating columns and flags.
    /// </summary>
    public class IndexDefinition
    {
        public string IndexName { get; set; } = string.Empty;
        public List<string> ColumnNames { get; set; } = new();
        public bool IsUnique { get; set; }
        public bool IsClustered { get; set; }
    }

    /// <summary>
    /// Tracks a column's participation in an index.
    /// Multiple IndexParticipation entries on different columns form a composite index.
    /// </summary>
    public class IndexParticipation
    {
        public string IndexName { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsUnique { get; set; }
        public bool IsClustered { get; set; }
    }
}
