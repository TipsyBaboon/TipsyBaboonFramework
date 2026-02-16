using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("User")]
    [Index(nameof(UserId), nameof(LoginProvider), nameof(ProviderKey), IsUnique = true)]
    public class UserLogin : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [StringLength(450)]
        public string? LoginProvider { get; set; }
        [StringLength(450)]
        public string? ProviderKey { get; set; }
        public string? ProviderDisplayName { get; set; }

        [ForeignKey("User")]
        public Guid UserId { get; set; }
        
        public User? User { get; set; }
    }
}
