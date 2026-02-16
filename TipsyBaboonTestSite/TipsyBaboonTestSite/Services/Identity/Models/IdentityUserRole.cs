using System;

namespace TipsyBaboonTestSite.Services.Identity.Models
{
    public class IdentityUserRole
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public Guid? OwnerId { get; set; }
        public int OwnershipLevel { get; set; }
        public bool IsInvariant { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public IdentityUser User { get; set; } = null!;
        public IdentityRole Role { get; set; } = null!;
    }
}
