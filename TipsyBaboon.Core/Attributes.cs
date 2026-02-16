using System;


namespace TipsyBaboon.Core.Attributes
{

    #region Permissions Handling

    /// <summary>
    /// Specifies authorization requirements for a class, typically a model or view model. 
    /// Used to delegate permissions to a parent model, or to specify no permissions required.
    /// Use AuthorizeAs.None constant to indicate no permission checks are needed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AuthorizeAsAttribute : Attribute
    {
        public const string None = "__None__";

        public string ParentModelName { get; }

        public string? ParentModuleName { get; set; }

        public bool IsNone => ParentModelName == None;

        public AuthorizeAsAttribute(string parentModelName)
        {
            if (string.IsNullOrWhiteSpace(parentModelName))
                throw new ArgumentException("Parent model name cannot be empty.", nameof(parentModelName));

            ParentModelName = parentModelName;
        }
    }

    /// <summary>
    /// Specifies the allowed ownership levels for a model class.
    /// Default is Public only if no attribute is present.
    /// Private: user can only see their own records.
    /// Group: anyone in the user's group can see the records.
    /// Public: accessible to everyone.
    /// Framework models should always be Public only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class OwnershipLevelsAttribute : Attribute
    {
        public OwnershipLevel[] AllowedLevels { get; }

        public OwnershipLevelsAttribute(params OwnershipLevel[] allowedLevels)
        {
            if (allowedLevels == null || allowedLevels.Length == 0)
                throw new ArgumentException("At least one ownership level must be specified.", nameof(allowedLevels));

            AllowedLevels = allowedLevels;
        }
    }

    /// <summary>
    /// Declares privilege availability for a model, for authorization and access control. Can be used multiple times per class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PrivilegeAttribute : Attribute
    {
        public string Name { get; }

        public string? Description { get; set; }

        public PrivilegeAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Privilege name cannot be empty.", nameof(name));

            Name = name;
        }
    }

    
    /// <summary>
    /// Marks a property as requiring a specific privilege for visibility and editing.
    /// The property is hidden from users whose roles lack the named privilege.
    /// Privileges are declared on the model class via <see cref="PrivilegeAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class RequirePrivilegeAttribute : Attribute
    {
        public string PrivilegeName { get; }
        public RequirePrivilegeAttribute(string privilegeName)
        {
            if (string.IsNullOrWhiteSpace(privilegeName))
                throw new ArgumentException("Privilege name cannot be empty.", nameof(privilegeName));
            PrivilegeName = privilegeName;
        }
    }

    #endregion

    #region UI Helper Attributes

    #region Class Level 

    /// <summary>
    /// Specifies the module name for a class, typically used for module registration and discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ModuleNameAttribute : Attribute
    {
        public string Name { get; }

        public ModuleNameAttribute(string moduleName)
        {
            Name = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
        }
    }

    /// <summary>
    /// Marks a class as a UI entity, providing metadata for UI generation, menu display, and grouping.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class UIEntityAttribute : Attribute
    {
        public string ModuleName { get; set; } = "TipsyBaboon.Core";

        public string Name { get; set; }

        public string? PluralName { get => field ?? Name + "s"; set => field = value; } = null;

        public string? Description { get; set; }

        public string? Icon { get; set; }

        public string? GroupName { get; set; }

        public int Order { get; set; } = 10000;

        public bool ShowInMenu { get; set; } = true;

        public bool HasOwnership { get; set; } = true;

        public string[] ScriptPaths { get; set; } = [];

        public string[] StylePaths { get; set; } = [];
        // if none defined, default to RecordName ASC, if no RecordName defined, then CreatedDate DESC
        public string[] DefaultSort { get; set; } = [];

        public bool CanLayout { get; set; } = true;

        public UIEntityAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// Marks a class as a configuration class for module configuration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConfigurationClassAttribute : Attribute
    {

    }

    /// <summary>
    /// Marks a class as a configuration class for module user preferences.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class UserPreferenceClassAttribute : Attribute
    {

    }

    /// <summary>
    /// Groups UI elements for a class, providing metadata for grouping and display in the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class UIGroupAttribute : Attribute
    {
        public string Name { get; }

        public string Title { get; set; }

        public string? Description { get; set; }

        public int Order { get; set; }

        public bool Collapsible { get; set; }

        public bool InitiallyCollapsed { get; set; }

        public string? CssClass { get; set; }

        public UIGroupAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Title = name;
        }
    }

    /// <summary>
    /// Forces records of this type to open in full-screen page mode for specified operations.
    /// Applies to grids, lookup fields, and any navigation to this record type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FullScreenOnlyAttribute : Attribute
    {
        /// <summary>
        /// Specifies which operations require full-screen pages.
        /// </summary>
        public FullScreenMode Mode { get; }

        /// <summary>
        /// Creates a FullScreenOnly attribute with the specified mode.
        /// </summary>
        /// <param name="mode">Which operations require full-screen (Create, Detail, or Both)</param>
        public FullScreenOnlyAttribute(FullScreenMode mode = FullScreenMode.Both)
        {
            Mode = mode;
        }
    }

    #endregion

    /// <summary>
    /// Marks a class or property as read-only in the UI, preventing editing in forms or grids.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
    public class UIReadOnlyAttribute : Attribute
    {
    }

    #region Property Level

    /// <summary>
    /// Provides display metadata for a property in the UI, such as name, description, and formatting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UIDisplayAttribute : Attribute
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Prompt { get; set; }

        public string? GroupName { get; set; }

        /// <summary>
        /// Display order in default layout. Use -1 to exclude from default layout while keeping available for custom layouts.
        /// </summary>
        public int Order { get; set; } = -1;

        public int ColumnSize { get; set; } = 6;

        public bool ShowInList { get; set; } = false;

        public bool ShowInDetail { get; set; } = true;

        public bool ShowLabel { get; set; } = true;

    }

    /// <summary>
    /// Specifies a custom control name for a property in the UI. Used for UI rendering customization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FieldControlAttribute : Attribute
    {
        public string Name { get; }

        /// <summary>
        /// Optional configuration as JSON string.
        /// Example: {"EditableColumns":["ProductId","Quantity"],"MaxHeight":"400px"}
        /// </summary>
        public string? ConfigJson { get; set; }
        
        /// <summary>
        /// Creates a FieldControl attribute with optional JSON configuration.
        /// </summary>
        /// <param name="controlName">The control name to use for rendering</param>
        /// <param name="configJson">Optional JSON configuration string</param>
        public FieldControlAttribute(string controlName, string? configJson = null)
        {
            if (string.IsNullOrWhiteSpace(controlName))
                throw new ArgumentException("Control name cannot be null or empty.", nameof(controlName));

            Name = controlName;
            ConfigJson = configJson;
        }
    }

    /// <summary>
    /// Marks a property as the record name, used for display or identification purposes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RecordNameAttribute : Attribute
    {
        public RecordNameAttribute() { }
    }

    #endregion

    /// <summary>
    /// Declares a UI action for a method, specifying display and behavior metadata for UI buttons or actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UIActionAttribute : Attribute
    {
        public string DisplayName { get; }

        public string? Description { get; set; }

        public string? Icon { get; set; }

        public string? ButtonClass { get; set; } = "btn-primary";

        public int Order { get; set; } = 100;

        public bool RequireConfirmation { get; set; }

        public string? ConfirmationMessage { get; set; }

        #region Used for determining permissions

        // optional: assume current model and module if not specified, otherwise use for permission checks
        public string? ModuleName { get; set; }
        public string? ModelName { get; set; }
        public bool Visible { get; set; } = true;

        // Note: Actions May Require Privilege and/or Permissions for the record (or neither).  If both are specified, both must be satisfied.
        //  If neither is specified, the action is allowed for all users with access to the record.

        // optional: if specified, requires the privilege for the current model or the alternate model if ModuleName/ModelName are specified..
        public string? Privilege { get; set; }
        // optional: if specified, requires the permission level for the current model or the alternate model if ModuleName/ModelName are specified..
        public ActionType? PermissionLevel { get; set; }

        #endregion

        // default modal message dialog if JSFunction is not provided, otherwise call the js function with the ApiResponse
        public string? JSFunction { get; set; }

        public UIActionAttribute(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

            DisplayName = displayName;
        }
    }

    #endregion

    #region Sql helper attributes

    /// <summary>
    /// Used to greedily include navigation properties in queries. Can be applied to properties to control inclusion in grid/detail views.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public class IncludeNavigationAttribute : Attribute
    {
        public bool InGrid { get; set; } = false;
        public bool InDetail { get; set; } = true;

        public IncludeNavigationAttribute()
        {
        }

    }


    /// <summary>
    /// Marks a class as a view model, providing metadata for source tables and description.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ViewModelAttribute : Attribute
    {
        public string[]? SourceTables { get; set; }

        public string? Description { get; set; }
    }

    /// <summary>
    /// Defines an index on a class or property, matching EF Core IndexAttribute design without dependency.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class IndexAttribute : Attribute
    {
        public string? Name { get; set; }
        
        public string[]? PropertyNames { get; set; }
        
        public bool IsUnique { get; set; }
        
        public bool IsClustered { get; set; }
        
        public int Order { get; set; }

        public IndexAttribute()
        {
        }

        public IndexAttribute(params string[] propertyNames)
        {
            PropertyNames = propertyNames;
        }
    }

    /// <summary>
    /// Defines a composite primary key on a class, matching EF Core 7.0+ PrimaryKeyAttribute design without dependency.
    /// Used for view models and entities with composite keys.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class PrimaryKeyAttribute : Attribute
    {
        public string[] PropertyNames { get; }

        public PrimaryKeyAttribute(params string[] propertyNames)
        {
            if (propertyNames == null || propertyNames.Length == 0)
                throw new ArgumentException("At least one property name must be specified for composite key.", nameof(propertyNames));

            PropertyNames = propertyNames;
        }
    }

    /// <summary>
    /// Marks a foreign key property to cascade delete to child records when the parent is deleted.
    /// Apply this attribute to foreign key properties (e.g., ParentId) to enable ON DELETE CASCADE in the database.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CascadeDeleteAttribute : Attribute
    {
    }

    #endregion

    #region Invariant Records & Seed Data

    /* INVARIANT RECORDS
     * 
     * Attribute-based User-Defined Invariants (e.g., Role model with [GenerateInvariantRecords])
     * - Declared via static property with [GenerateInvariantRecords] attribute
     * - Example: Role.GetInvariantRecords returns Admin, Anonymous, BasicRights roles
     * - Code-locked: IsInvariant = true, deterministic Guids
     * - Maintained at runtime: upserted on every startup
     * - Defined by application developers for business-critical records
     * 
     * Framework-level invariants (ModelRecord, Privilege, etc.) are handled by SqlServer
     * provider from schema definitions and registration.
     */

    /// <summary>
    /// Marks a static property that returns invariant records to be generated and maintained.
    /// Invariant records are code-locked (IsInvariant = true) with deterministic Guids.
    /// They are upserted on every startup to ensure system integrity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class GenerateInvariantRecordsAttribute : Attribute
    {

    }

    /// <summary>
    /// Marks a static property that returns seed records to be inserted at table creation only.
    /// Seed records are not locked, not maintained, and not updated - they provide initial data only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class GenerateSeedRecordsAttribute : Attribute
    {

    }

    #endregion

}
