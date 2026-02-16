using System;
using System.Collections.Generic;

namespace TipsyBaboonTestSite.Services.Identity.Models
{
    public class IdentityUser
    {
        public Guid Id { get; set; }
        public Guid? OwnerId { get; set; }
        public int OwnershipLevel { get; set; }
        public bool IsInvariant { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string? UserName { get; set; }
        public string? NormalizedUserName { get; set; }
        public string? Email { get; set; }
        public string? NormalizedEmail { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PasswordHash { get; set; }
        public string? SecurityStamp { get; set; }
        public string? ConcurrencyStamp { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; }
        public int AccessFailedCount { get; set; }

        public ICollection<IdentityUserRole> UserRoles { get; set; } = new List<IdentityUserRole>();
        public ICollection<IdentityUserClaim> Claims { get; set; } = new List<IdentityUserClaim>();
        public ICollection<IdentityUserLogin> Logins { get; set; } = new List<IdentityUserLogin>();
        public ICollection<IdentityUserToken> Tokens { get; set; } = new List<IdentityUserToken>();
    }
}
