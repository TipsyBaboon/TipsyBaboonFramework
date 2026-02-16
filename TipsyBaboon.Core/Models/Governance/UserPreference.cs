using System;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [Index(nameof(UserId), nameof(Key), IsUnique = true)]
    [OwnershipLevels(OwnershipLevel.Private)]
    [AuthorizeAs(AuthorizeAsAttribute.None)]
    public class UserPreferenceItem : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(250)]
        public string Key { get; set; } = string.Empty;
        
        public string? Value { get; set; }
    }
}
