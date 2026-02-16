// Consolidated enums for TipsyBaboon.Core
// This file contains all enums from the previous Models/, Data/, and Governance/ folders.

namespace TipsyBaboon.Core
{
    /// <summary>
    /// Unified CRUD operation types used throughout the system for permission and action tracking.
    /// </summary>
    public enum ActionType
    {
        /// <summary>Create operation</summary>
        Create = 0,
        /// <summary>Read operation</summary>
        Read = 1,
        /// <summary>Update operation</summary>
        Update = 2,
        /// <summary>Delete operation</summary>
        Delete = 3
    }

    /// <summary>
    /// Response types for save operations in BeforeChangeAsync hooks.
    /// </summary>
    public enum SaveResponseType
    {
        /// <summary>Continue with the save operation</summary>
        Continue,
        /// <summary>Change already applied by hook; skip further processing</summary>
        ChangeApplied,
        /// <summary>Error occurred; abort save operation</summary>
        Error,
    }

    /// <summary>
    /// Permission scope levels for record access control.
    /// </summary>
    public enum PermissionLevel
    {
        /// <summary>No access</summary>
        None = 0,
        /// <summary>Access to own records only</summary>
        Own = 1,
        /// <summary>Access to records within user's group</summary>
        Group = 2,
        /// <summary>Access to all records</summary>
        All = 3
    }

    /// <summary>
    /// Ownership visibility levels for records.
    /// </summary>
    public enum OwnershipLevel
    {
        /// <summary>Visible only to owner</summary>
        Private = 0,
        /// <summary>Visible to owner's group members</summary>
        Group = 1,
        /// <summary>Visible to all users</summary>
        Public = 2
    }

    /// <summary>
    /// Menu item types for navigation menu construction.
    /// </summary>
    public enum MenuItemType
    {
        /// <summary>Link to a registered model's list page</summary>
        ModelLink,
        /// <summary>Custom external or internal link</summary>
        CustomLink,
        /// <summary>Collapsible group of menu items</summary>
        Group,
        /// <summary>Section header (non-clickable)</summary>
        Header
    }

    /// <summary>
    /// Filter comparison operators for query building.
    /// </summary>
    public enum FilterOperator
    {
        /// <summary>Exact equality</summary>
        Equals,
        /// <summary>Not equal</summary>
        NotEquals,
        /// <summary>Greater than</summary>
        GreaterThan,
        /// <summary>Greater than or equal</summary>
        GreaterThanOrEqual,
        /// <summary>Less than</summary>
        LessThan,
        /// <summary>Less than or equal</summary>
        LessThanOrEqual,
        /// <summary>String contains substring</summary>
        Contains,
        /// <summary>String starts with prefix</summary>
        StartsWith,
        /// <summary>String ends with suffix</summary>
        EndsWith,
        /// <summary>Value in list</summary>
        In,
        /// <summary>Value not in list</summary>
        NotIn,
        /// <summary>Value between two bounds</summary>
        Between,
        /// <summary>Value is null</summary>
        IsNull,
        /// <summary>Value is not null</summary>
        IsNotNull
    }

    /// <summary>
    /// Logical operators for combining filter criteria.
    /// </summary>
    public enum LogicalOperator
    {
        /// <summary>All conditions must be true</summary>
        And,
        /// <summary>At least one condition must be true</summary>
        Or
    }

    /// <summary>
    /// Sort direction for query results.
    /// </summary>
    public enum SortDirection
    {
        /// <summary>Ascending order</summary>
        Ascending,
        /// <summary>Descending order</summary>
        Descending
    }

    /// <summary>
    /// SQL join types for view and query definitions.
    /// </summary>
    public enum JoinType
    {
        /// <summary>Cross join (Cartesian product)</summary>
        Cross,
        /// <summary>Inner join</summary>
        Inner,
        /// <summary>Left outer join</summary>
        Left,
        /// <summary>Right outer join</summary>
        Right,
        /// <summary>Full outer join</summary>
        Full
    }

    /// <summary>
    /// Types of database schema differences detected during sync.
    /// </summary>
    public enum SchemaDifferenceType
    {
        /// <summary>Table missing from database</summary>
        TableMissing,
        /// <summary>Column missing from table</summary>
        ColumnMissing,
        /// <summary>Column type mismatch</summary>
        ColumnTypeMismatch,
        /// <summary>Column length mismatch</summary>
        ColumnLengthMismatch,
        /// <summary>Extra column in database not in model</summary>
        ExtraColumn
    }

    /// <summary>
    /// Layout context types for model form layouts.
    /// Allows different field arrangements per CRUD operation.
    /// </summary>
    public enum LayoutType
    {
        /// <summary>Default layout used when no context-specific layout exists</summary>
        Default = 0,
        /// <summary>Layout for Create forms</summary>
        Create = 1,
        /// <summary>Layout for Read-only detail views</summary>
        Read = 2,
        /// <summary>Layout for Update/Edit forms</summary>
        Update = 3
    }

    /// <summary>
    /// Specifies which operations must use full-screen pages instead of modals.
    /// </summary>
    [Flags]
    public enum FullScreenMode
    {
        /// <summary>No full-screen requirement - allow modals</summary>
        None = 0,
        /// <summary>Create operations must use full-screen pages</summary>
        Create = 1,
        /// <summary>Detail (view/edit) operations must use full-screen pages</summary>
        Detail = 2,
        /// <summary>Both create and detail operations must use full-screen pages</summary>
        Both = Create | Detail
    }
}
