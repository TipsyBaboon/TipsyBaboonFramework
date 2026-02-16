using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("User")]
    [Index(nameof(UserId), nameof(ClaimType))]
    public class UserClaim : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("User")]
        public Guid UserId { get; set; }
        
        [StringLength(450)]
        public string? ClaimType { get; set; }
        public string? ClaimValue { get; set; }

        public User? User { get; set; }
    }
}
