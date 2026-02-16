using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [UIEntity("Layout", ModuleName = "Governance", GroupName = "Governance", Order = 85, Icon = "bi bi-layout-text-sidebar-reverse", HasOwnership = false, ShowInMenu = false)]
    [FullScreenOnly(FullScreenMode.Detail)]
    [UIGroup("Layout", Title = "Layout Details", Order = 10)]
    [Index(nameof(ModelRecordId), nameof(LayoutType), IsUnique = true)]
    public class Layout : TipsyBaboonModel
    {
        public Layout()
        {
            OwnershipLevel = OwnershipLevel.Public;
        }

        [Key]
        [UIDisplay(Order = -1, ShowInList = false, ShowInDetail = true)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [UIDisplay(Name = "Layout Type", GroupName = "Layout", Order = 20, ColumnSize = 6, ShowInList = true)]
        public LayoutType LayoutType { get; set; } = LayoutType.Default;

        [Required]
        [ForeignKey(nameof(ModelRecord))]
        [UIDisplay(Name = "Model", GroupName = "Layout", Order = 30, ColumnSize = 12, ShowInList = false, ShowInDetail = false)]
        public Guid ModelRecordId { get; set; }

        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public List<LayoutSection> Sections { get; set; } = new List<LayoutSection>();
    }
}
