using System;
using System.Collections.Generic;

namespace TipsyBaboon.Core.Data
{
    /// <summary>
    /// Filter criteria for querying records.
    /// Provides fluent static factory methods for common comparison operations.
    /// </summary>
    public class FilterCriteria
    {
        /// <summary>
        /// Property name to filter on.
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Comparison operator (Equals, Contains, GreaterThan, etc.).
        /// </summary>
        public FilterOperator Operator { get; set; } = FilterOperator.Equals;

        /// <summary>
        /// Value or values to compare against.
        /// For Between operator, use a tuple (lower, upper).
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Logical operator for combining with other filters (And, Or).
        /// </summary>
        public LogicalOperator Logic { get; set; } = LogicalOperator.And;

        /// <summary>
        /// Creates an equality filter.
        /// </summary>
        public static FilterCriteria Equals(string property, object? value)
            => new() { PropertyName = property, Operator = FilterOperator.Equals, Value = value };

        /// <summary>
        /// Creates a not equals filter.
        /// </summary>
        public static FilterCriteria NotEquals(string property, object? value)
            => new() { PropertyName = property, Operator = FilterOperator.NotEquals, Value = value };

        /// <summary>
        /// Creates a contains filter for string properties.
        /// </summary>
        public static FilterCriteria Contains(string property, string value)
            => new() { PropertyName = property, Operator = FilterOperator.Contains, Value = value };

        /// <summary>
        /// Creates a starts with filter for string properties.
        /// </summary>
        public static FilterCriteria StartsWith(string property, string value)
            => new() { PropertyName = property, Operator = FilterOperator.StartsWith, Value = value };

        /// <summary>
        /// Creates an IN filter (value in list).
        /// </summary>
        public static FilterCriteria In<T>(string property, IEnumerable<T> values)
            => new() { PropertyName = property, Operator = FilterOperator.In, Value = values };

        /// <summary>
        /// Creates a greater than filter.
        /// </summary>
        public static FilterCriteria GreaterThan(string property, object value)
            => new() { PropertyName = property, Operator = FilterOperator.GreaterThan, Value = value };

        /// <summary>
        /// Creates a greater than or equal filter.
        /// </summary>
        public static FilterCriteria GreaterThanOrEqual(string property, object value)
            => new() { PropertyName = property, Operator = FilterOperator.GreaterThanOrEqual, Value = value };

        /// <summary>
        /// Creates a less than filter.
        /// </summary>
        public static FilterCriteria LessThan(string property, object value)
            => new() { PropertyName = property, Operator = FilterOperator.LessThan, Value = value };

        /// <summary>
        /// Creates a less than or equal filter.
        /// </summary>
        public static FilterCriteria LessThanOrEqual(string property, object value)
            => new() { PropertyName = property, Operator = FilterOperator.LessThanOrEqual, Value = value };

        /// <summary>
        /// Creates a between filter (inclusive range).
        /// </summary>
        public static FilterCriteria Between(string property, object lower, object upper)
            => new() { PropertyName = property, Operator = FilterOperator.Between, Value = (lower, upper) };

        /// <summary>
        /// Creates an IS NULL filter.
        /// </summary>
        public static FilterCriteria IsNull(string property)
            => new() { PropertyName = property, Operator = FilterOperator.IsNull };

        /// <summary>
        /// Creates an IS NOT NULL filter.
        /// </summary>
        public static FilterCriteria IsNotNull(string property)
            => new() { PropertyName = property, Operator = FilterOperator.IsNotNull };
    }

    /// <summary>
    /// Sort criteria for query result ordering.
    /// Provides fluent static factory methods for ascending and descending sorts.
    /// </summary>
    public class SortCriteria
    {
        /// <summary>
        /// Property name to sort by.
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Sort direction (Ascending or Descending).
        /// </summary>
        public SortDirection Direction { get; set; } = SortDirection.Ascending;

        /// <summary>
        /// Creates an ascending sort.
        /// </summary>
        public static SortCriteria Asc(string property)
            => new() { PropertyName = property, Direction = SortDirection.Ascending };

        /// <summary>
        /// Creates a descending sort.
        /// </summary>
        public static SortCriteria Desc(string property)
            => new() { PropertyName = property, Direction = SortDirection.Descending };
    }
}
