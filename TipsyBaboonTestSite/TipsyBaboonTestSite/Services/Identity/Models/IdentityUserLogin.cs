using System;

namespace TipsyBaboonTestSite.Services.Identity.Models
{
    public class IdentityUserLogin
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? OwnerId { get; set; }
        public int OwnershipLevel { get; set; }
        public bool IsInvariant { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string LoginProvider { get; set; } = string.Empty;
        public string ProviderKey { get; set; } = string.Empty;
        public string? ProviderDisplayName { get; set; }

        public IdentityUser User { get; set; } = null!;
    }
}
