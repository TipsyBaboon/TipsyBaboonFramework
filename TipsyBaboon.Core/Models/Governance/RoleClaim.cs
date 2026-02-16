using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("Role")]
    [Index(nameof(RoleId), nameof(ClaimType))]
    public class RoleClaim : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Role")]
        public Guid RoleId { get; set; }
        
        [StringLength(450)]
        public string? ClaimType { get; set; }
        public string? ClaimValue { get; set; }

        public Role? Role { get; set; }
    }
}
