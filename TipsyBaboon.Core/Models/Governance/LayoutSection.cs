using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Customized section grouping definition for form layouts.
    /// Groups related properties together with optional collapse behavior and styling.
    /// Used by the layout editor to persist custom UI section arrangements.
    /// </summary>
    [ModuleName("Governance")]
    [AuthorizeAs("Layout")]
    public class LayoutSection : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey(nameof(Layout))]
        [CascadeDelete]
        public Guid LayoutId { get; set; }

        public string Name { get; set; } = string.Empty;

        [RecordName]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int Order { get; set; } = 100;

        public bool IsCollapsible { get; set; }

        public bool IsInitiallyCollapsed { get; set; }

        public int ColumnSize { get; set; } = 12;

        public string? CssClass { get; set; }
        public List<LayoutProperty> Properties { get; set; } = new List<LayoutProperty>();
    }
}
