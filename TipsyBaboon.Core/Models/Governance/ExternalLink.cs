using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// External link definition for menu navigation.
    /// Links can be assigned to specific roles and displayed in the navigation menu.
    /// </summary>
    [UIEntity("External Link", ModuleName = "Governance", GroupName = "Governance", Order = 70, Icon = "bi bi-link-45deg", HasOwnership = false)]
    [UIGroup("Basic", Title = "Link Information", Order = 10)]
    [UIGroup("Relationships", Title = "Relationships", Order = 20, Collapsible = true, InitiallyCollapsed = true)]
    [Index(nameof(Title), IsUnique = true)]
    [OwnershipLevels(TipsyBaboon.Core.OwnershipLevel.Public)]
    public class ExternalLink : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [RecordName]
        [Required]
        [StringLength(256)]
        [UIDisplay(Name = "Title", GroupName = "Basic", Order = 10, ColumnSize = 6, ShowInList = true)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2048)]
        [UIDisplay(Name = "URL", GroupName = "Basic", Order = 20, ColumnSize = 6, ShowInList = true)]
        public string Url { get; set; } = string.Empty;

        [UIDisplay(Name = "Description", GroupName = "Basic", Order = 30, ColumnSize = 12, ShowInList = false, ShowInDetail = true)]
        public string? Description { get; set; }

        [UIDisplay(Name = "Icon", GroupName = "Basic", Order = 40, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        [StringLength(100)]
        public string? Icon { get; set; }

        [UIDisplay(Name = "Open in New Tab", GroupName = "Basic", Order = 50, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        public bool OpenInNewTab { get; set; } = true;

        [UIDisplay(Name = "Menu Order", GroupName = "Basic", Order = 60, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        public int MenuOrder { get; set; } = 0;

        public List<RoleExternalLink> RoleExternalLinks { get; set; } = new List<RoleExternalLink>();

        [UIDisplay(Name = "Role Assignments", GroupName = "Relationships", Order = 11, ShowInList = false)]
        public List<ExternalLinkRoleAssignment> ExternalLinkRoleAssignments { get; set; } = new List<ExternalLinkRoleAssignment>();
    }
}
