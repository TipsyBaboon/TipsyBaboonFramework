using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Customized property display settings within a layout section.
    /// Allows users to override column order, size, and CSS classes per property.
    /// Used by the layout editor to persist custom UI arrangements.
    /// </summary>
    [ModuleName("Governance")]
    [AuthorizeAs("Layout")]
    public class LayoutProperty : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey(nameof(LayoutSection))]
        [CascadeDelete]
        public Guid LayoutSectionId { get; set; }

        [RecordName]
        public string PropertyName { get; set; } = string.Empty;

        public int Order { get; set; } = 10000;


        public int ColumnSize { get; set; } = 12;

        public string? CssClass { get; set; }

        public bool ShowLabel { get; set; } = true;
    }
}
