namespace TipsyBaboon.Core.Data
{
    /// <summary>
    /// Result of a database schema synchronization operation.
    /// Contains applied changes, warnings, errors, and statistics.
    /// </summary>
    public class SchemaSyncResult
    {
        /// <summary>
        /// Whether the sync operation completed successfully.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// List of schema differences that were applied.
        /// </summary>
        public List<SchemaDifference> AppliedChanges { get; set; } = new();
        /// <summary>
        /// Non-fatal warnings encountered during sync.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
        /// <summary>
        /// Errors that prevented full synchronization.
        /// </summary>
        public List<string> Errors { get; set; } = new();
        /// <summary>
        /// Number of tables created during sync.
        /// </summary>
        public int TablesCreated { get; set; }
        /// <summary>
        /// Number of columns added during sync.
        /// </summary>
        public int ColumnsAdded { get; set; }
        /// <summary>
        /// Number of columns modified (type or length changed) during sync.
        /// </summary>
        public int ColumnsModified { get; set; }
    }

    /// <summary>
    /// Represents a single difference between the model definition and database schema.
    /// </summary>
    public class SchemaDifference
    {
        /// <summary>
        /// Model type name.
        /// </summary>
        public string ModelTypeName { get; set; } = string.Empty;
        /// <summary>
        /// Database table name.
        /// </summary>
        public string TableName { get; set; } = string.Empty;
        /// <summary>
        /// Property name (for column-level differences).
        /// </summary>
        public string? PropertyName { get; set; }
        /// <summary>
        /// Database column name (for column-level differences).
        /// </summary>
        public string? ColumnName { get; set; }
        /// <summary>
        /// Type of schema difference detected.
        /// </summary>
        public SchemaDifferenceType DifferenceType { get; set; }
        /// <summary>
        /// Expected column type from model definition.
        /// </summary>
        public string? ExpectedType { get; set; }
        /// <summary>
        /// Actual column type in the database.
        /// </summary>
        public string? ActualType { get; set; }
        /// <summary>
        /// Expected max length from model definition.
        /// </summary>
        public int ExpectedMaxLength { get; set; }
        /// <summary>
        /// Actual max length in the database.
        /// </summary>
        public int ActualMaxLength { get; set; }
        /// <summary>
        /// Human-readable description of the difference.
        /// </summary>
        public string? Description { get; set; }
    }
}
