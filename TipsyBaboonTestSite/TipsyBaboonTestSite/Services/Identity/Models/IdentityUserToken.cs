using System;

namespace TipsyBaboonTestSite.Services.Identity.Models
{
    public class IdentityUserToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? OwnerId { get; set; }
        public int OwnershipLevel { get; set; }
        public bool IsInvariant { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string LoginProvider { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }

        public IdentityUser User { get; set; } = null!;
    }
}
