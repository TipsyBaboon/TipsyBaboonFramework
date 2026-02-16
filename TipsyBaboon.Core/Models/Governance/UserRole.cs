using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Junction table linking Users to Roles for role-based authorization.
    /// Composite primary key on (UserId, RoleId).
    /// </summary>
    [ModuleName("Governance")]
    [AuthorizeAs("User")]
    [PrimaryKey(nameof(UserId), nameof(RoleId))]
    public class UserRole : TipsyBaboonModel
    {
        [ForeignKey("User")]
        public Guid UserId { get; set; }
        
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }

        public User? User { get; set; }
        public Role? Role { get; set; }
    }
}
