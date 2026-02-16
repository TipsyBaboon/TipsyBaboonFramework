using System.Collections.Generic;

namespace TipsyBaboon.Core.Data
{
    /// <summary>
    /// Declarative view definition for creating SQL views from multiple tables.
    /// Used with [ViewModel] attribute and IViewModelStore for read-only multi-table queries.
    /// </summary>
    public class ViewDefinition
    {
        /// <summary>
        /// Tables to include in the view.
        /// </summary>
        public List<TableSource> Tables { get; set; } = new();

        /// <summary>
        /// Join conditions between tables.
        /// </summary>
        public List<JoinDefinition> Joins { get; set; } = new();

        /// <summary>
        /// Mappings from SQL expressions to model properties.
        /// </summary>
        public List<ColumnMapping> Columns { get; set; } = new();

        /// <summary>
        /// Optional WHERE clause for filtering view results.
        /// </summary>
        public string? WhereClause { get; set; }
    }

    /// <summary>
    /// Table source for a view definition.
    /// </summary>
    public class TableSource
    {
        /// <summary>
        /// Database table name.
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Alias for the table in SQL queries.
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Join type (Cross, Inner, Left, Right, Full).
        /// </summary>
        public JoinType JoinType { get; set; } = JoinType.Cross;
    }

    /// <summary>
    /// Join condition between two tables in a view.
    /// </summary>
    public class JoinDefinition
    {
        /// <summary>
        /// Left table alias.
        /// </summary>
        public string LeftTable { get; set; } = string.Empty;

        /// <summary>
        /// Left table column name.
        /// </summary>
        public string LeftColumn { get; set; } = string.Empty;

        /// <summary>
        /// Right table alias.
        /// </summary>
        public string RightTable { get; set; } = string.Empty;

        /// <summary>
        /// Right table column name.
        /// </summary>
        public string RightColumn { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maps a SQL expression to a model property in a view.
    /// </summary>
    public class ColumnMapping
    {
        /// <summary>
        /// SQL expression or column reference (e.g., "u.Email", "COUNT(*)").
        /// </summary>
        public string SourceExpression { get; set; } = string.Empty;

        /// <summary>
        /// Target property name on the view model.
        /// </summary>
        public string TargetProperty { get; set; } = string.Empty;
    }
}
