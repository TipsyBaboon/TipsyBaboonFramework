using System;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [UIReadOnly]
    public class ChangeLog : TipsyBaboonModel
    {
        [Key]
        [UIDisplay(Order = -1, ShowInList = false, ShowInDetail = false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [UIDisplay(Name = "Date/Time", Order = 1, ShowInList = true, ShowInDetail = false)]
        public override DateTime CreatedOn { get; set; }

        [UIDisplay(Name = "Changed By", Order = 2, ShowInList = true, ShowInDetail = false)]
        public string? ChangedByDisplayName { get; set; }

        [UIDisplay(Name = "Module", Order = 3, ShowInList = true, ShowInDetail = false)]
        public string? ModuleName { get; set; }

        [UIDisplay(Name = "Model", Order = 4, ShowInList = true, ShowInDetail = false)]
        public string? ModelTypeName { get; set; }

        [UIDisplay(Name = "Record ID", Order = 5, ShowInList = true, ShowInDetail = false)]
        public string? RecordId { get; set; }

        [UIDisplay(Name = "Record Name", Order = 6, ShowInList = true, ShowInDetail = false)]
        public string? RecordName { get; set; }

        [UIDisplay(Name = "Action", Order = 7, ShowInList = true, ShowInDetail = false)]
        public ActionType ActionType { get; set; }

        [UIDisplay(Order = -1, ShowInList = false, ShowInDetail = false)]
        public Guid? ChangedById { get; set; }

        [UIDisplay(Name = "Description", Order = 8, ShowInList = true, ShowInDetail = false)]
        public string DescriptionOfChange { get; set; } = string.Empty;

    }
}
