using System.Collections.Generic;

namespace TipsyBaboon.Core.Data
{
    /// <summary>
    /// Request parameters for querying paged data with filtering, sorting, and navigation property inclusion.
    /// Provides fluent methods for building complex queries.
    /// </summary>
    public class PagedRequest
    {
        /// <summary>
        /// Page number (1-based). Null for unpaged results.
        /// </summary>
        public int? Page { get; set; }

        /// <summary>
        /// Page size (records per page). Null for unpaged results.
        /// </summary>
        public int? PageSize { get; set; }

        /// <summary>
        /// Filter criteria to apply to the query.
        /// </summary>
        public List<FilterCriteria> Filters { get; set; } = new();

        /// <summary>
        /// Sort criteria for ordering results.
        /// </summary>
        public List<SortCriteria> Sorts { get; set; } = new();

        /// <summary>
        /// Navigation property paths to eagerly load (e.g., "Owner", "CreatedByUser.Roles").
        /// </summary>
        public List<string> Includes { get; set; } = new();

        /// <summary>
        /// Whether to automatically include navigation properties marked with [IncludeNavigation].
        /// </summary>
        public bool UseAutoIncludes { get; set; } = false;

        /// <summary>
        /// Creates a paged request with optional page and page size.
        /// </summary>
        public static PagedRequest Create(int? page = null, int? pageSize = null)
            => new() { Page = page, PageSize = pageSize };

        /// <summary>
        /// Creates an unpaged request that returns all records.
        /// </summary>
        public static PagedRequest All => new();

        /// <summary>
        /// Adds a filter to the request (fluent).
        /// </summary>
        public PagedRequest WithFilter(FilterCriteria filter)
        {
            Filters.Add(filter);
            return this;
        }

        /// <summary>
        /// Adds a sort to the request (fluent).
        /// </summary>
        public PagedRequest WithSort(SortCriteria sort)
        {
            Sorts.Add(sort);
            return this;
        }

        /// <summary>
        /// Adds a navigation property include to the request (fluent).
        /// </summary>
        public PagedRequest WithInclude(string navigationPath)
        {
            Includes.Add(navigationPath);
            return this;
        }

        /// <summary>
        /// Sets paging parameters (fluent).
        /// </summary>
        public PagedRequest WithPaging(int page, int pageSize)
        {
            Page = page;
            PageSize = pageSize;
            return this;
        }

        /// <summary>
        /// Calculated record offset for SQL OFFSET clause.
        /// </summary>
        public int Offset => Page.HasValue && PageSize.HasValue ? (Page.Value - 1) * PageSize.Value : 0;

        public bool IsPaged => Page.HasValue && PageSize.HasValue;
    }
}
