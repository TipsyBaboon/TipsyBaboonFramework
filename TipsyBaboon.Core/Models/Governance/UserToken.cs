using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("User")]
    [Index(nameof(UserId), nameof(LoginProvider), nameof(Name), IsUnique = true)]
    public class UserToken : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("User")]
        public Guid UserId { get; set; }
        
        [StringLength(450)]
        public string? LoginProvider { get; set; }
        [StringLength(450)]
        public string? Name { get; set; }
        public string? Value { get; set; }

        public User? User { get; set; }
    }
}
