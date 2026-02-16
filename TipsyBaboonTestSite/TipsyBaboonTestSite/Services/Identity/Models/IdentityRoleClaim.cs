using System;

namespace TipsyBaboonTestSite.Services.Identity.Models
{
    public class IdentityRoleClaim
    {
        public Guid Id { get; set; }
        public Guid RoleId { get; set; }
        public Guid? OwnerId { get; set; }
        public int OwnershipLevel { get; set; }
        public bool IsInvariant { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string? ClaimType { get; set; }
        public string? ClaimValue { get; set; }

        public IdentityRole Role { get; set; } = null!;
    }
}
