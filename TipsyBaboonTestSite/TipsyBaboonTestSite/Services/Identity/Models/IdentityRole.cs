using System;
using System.Collections.Generic;

namespace TipsyBaboonTestSite.Services.Identity.Models
{
    public class IdentityRole
    {
        public Guid Id { get; set; }
        public Guid? OwnerId { get; set; }
        public int OwnershipLevel { get; set; }
        public bool IsInvariant { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string? Name { get; set; }
        public string? NormalizedName { get; set; }
        public string? ConcurrencyStamp { get; set; }

        public ICollection<IdentityUserRole> UserRoles { get; set; } = new List<IdentityUserRole>();
        public ICollection<IdentityRoleClaim> Claims { get; set; } = new List<IdentityRoleClaim>();
    }
}
