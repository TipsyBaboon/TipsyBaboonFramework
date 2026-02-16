using System;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.FrameworkBase;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Tracks an active user impersonation session.
    /// Allows administrators to temporarily log in as another user for support or testing.
    /// Sessions expire after a configured duration and are tracked in audit logs.
    /// </summary>
    [ModuleName("Governance")]
    public class ImpersonationSession : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ImpersonatingUserId { get; set; }

        [Required]
        public Guid ImpersonatedUserId { get; set; }

        [Required]
        public Guid ImpersonationKey { get; set; } = Guid.NewGuid();

        [Required]
        public DateTime ExpiresOn { get; set; }
    }
}
