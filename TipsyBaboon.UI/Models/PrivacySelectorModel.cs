using TipsyBaboon.Core;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// View model for the ownership/privacy selector control, providing the available ownership levels,
    /// the current user's group memberships, and the record's current ownership state.
    /// </summary>
    public class PrivacySelectorModel
    {
        public bool HasOwnership { get; set; }
        public List<OwnershipLevel> OwnershipLevels { get; set; } = new();
        public bool IsOwner { get; set; }
        public bool IsCreate { get; set; }
        public OwnershipLevel CurrentLevel { get; set; }
        public Guid? CurrentGroupId { get; set; }
        public string? CurrentGroupName { get; set; }
        public List<UserGroupInfo> UserGroups { get; set; } = new();
        public bool IsReadOnly { get; set; }
        public string? OwnerDisplayName { get; set; }
        public Guid? OwnerId { get; set; }
    }
}
