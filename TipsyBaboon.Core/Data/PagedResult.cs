using System.Collections.Generic;

namespace TipsyBaboon.Core.Data
{
    /// <summary>
    /// Result container for paged queries with metadata about pagination state.
    /// </summary>
    /// <typeparam name="T">Type of items in the result set.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Items in the current page.
        /// </summary>
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// Total count of items across all pages (before paging).
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number (1-based). Null for unpaged results.
        /// </summary>
        public int? Page { get; set; }

        /// <summary>
        /// Page size (items per page). Null for unpaged results.
        /// </summary>
        public int? PageSize { get; set; }

        /// <summary>
        /// Whether this result is paged (Page and PageSize are set).
        /// </summary>
        public bool IsPaged => Page.HasValue && PageSize.HasValue;

        /// <summary>
        /// Total number of pages.
        /// </summary>
        public int TotalPages => IsPaged && PageSize!.Value > 0 ? (TotalCount + PageSize.Value - 1) / PageSize.Value : 1;

        /// <summary>
        /// Whether there is a next page.
        /// </summary>
        public bool HasNextPage => IsPaged && Page < TotalPages;

        /// <summary>
        /// Whether there is a previous page.
        /// </summary>
        public bool HasPreviousPage => IsPaged && Page > 1;

        /// <summary>
        /// 1-based index of the first item in the current page.
        /// </summary>
        public int FirstItemIndex => TotalCount == 0 ? 0 : IsPaged ? (Page!.Value - 1) * PageSize!.Value + 1 : 1;

        /// <summary>
        /// 1-based index of the last item in the current page.
        /// </summary>
        public int LastItemIndex => TotalCount == 0 ? 0 : IsPaged ? System.Math.Min(Page!.Value * PageSize!.Value, TotalCount) : TotalCount;

        /// <summary>
        /// Creates an empty paged result with no items.
        /// </summary>
        public static PagedResult<T> Empty(int? page = null, int? pageSize = null)
            => new() { Page = page, PageSize = pageSize, TotalCount = 0 };

        /// <summary>
        /// Creates a paged result with items and total count.
        /// </summary>
        public static PagedResult<T> Create(List<T> items, int totalCount, int? page = null, int? pageSize = null)
            => new() { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }
}
