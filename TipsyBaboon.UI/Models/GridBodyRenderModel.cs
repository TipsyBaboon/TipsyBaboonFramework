using TipsyBaboon.Core.Data;

namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// Server-side render model for an individual grid body fragment (rows + paging),
    /// returned by the AJAX body endpoint to support paging without full page reload.
    /// </summary>
    public class GridBodyRenderModel
    {
        public string GridId { get; set; } = string.Empty;

        public string FormId { get; set; } = string.Empty;

        public SchemaDefinition Schema { get; set; } = null!;

        public List<GridColumn> Columns { get; set; } = new();

        public IEnumerable<object> Rows { get; set; } = Array.Empty<object>();

        public bool IsEditable { get; set; }

        public string? FieldName { get; set; }

        public string? ParentIdPropertyName { get; set; }

        public Guid? ParentEntityId { get; set; }

        public int CurrentPage { get; set; } = 1;

        public int TotalCount { get; set; }

        public int PageSize { get; set; } = 25;

        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;

        public bool EnableRowNavigation { get; set; } = true;
    }
}
