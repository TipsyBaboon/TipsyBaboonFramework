using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.Core.Models
{
    /// <summary>
    /// Abstract base class for all TipsyBaboon models.
    /// Provides ownership, audit trail, and invariant management properties.
    /// All application models should inherit from this class and define their own Id property.
    /// <para>
    /// Each model must declare its own Id property with the appropriate key type:
    /// - For new models: public Guid Id { get; set; } = Guid.NewGuid();
    /// - For legacy integration: public int Id { get; set; } with [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    /// </para>
    /// </summary>
    public abstract class TipsyBaboonModel
    {
        /// <summary>
        /// Ownership visibility level for this record (Private, Group, or Public).
        /// Defaults to Public for legacy data integration.
        /// </summary>
        [Column(TypeName = "int")]
        public OwnershipLevel OwnershipLevel { get; set; } = OwnershipLevel.Public;
        
        /// <summary>
        /// User group ID for group-level ownership.
        /// Used when OwnershipLevel is Group to restrict access to group members.
        /// </summary>
        public Guid? GroupId { get; set; } = null;
        
        /// <summary>
        /// Owner user ID for record ownership tracking.
        /// Used for Own-level permission filtering and Private ownership levels.
        /// </summary>
        public Guid? OwnerId { get; set; } = null;

        /// <summary>
        /// Holds the original state of the record before modifications.
        /// Used by BeforeChangeAsync hooks for comparing changes. Not mapped to database.
        /// </summary>
        [NotMapped]
        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public object? OriginalState { get; set; }

        /// <summary>
        /// Timestamp when this record was created (UTC).
        /// Automatically set by the framework on record creation.
        /// </summary>
        [UIReadOnly]
        [UIDisplay(Name = "Created On", ShowInList = true, ShowInDetail = false, Order = 9000)]
        [Column(TypeName = "datetime2")]
        public virtual DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID of the user who created this record.
        /// Automatically set by the framework on record creation.
        /// </summary>
        [UIReadOnly]
        public virtual Guid? CreatedById { get; set; }
        
        /// <summary>
        /// Display name of the user who created this record.
        /// Includes impersonation info if created during impersonation session.
        /// Automatically set by the framework on record creation.
        /// </summary>
        [UIReadOnly]
        [UIDisplay(ShowInList = true, Name = "Created By", ShowInDetail = false, Order = 9010)]
        public virtual string? CreatedByDisplayName { get; set; }

        /// <summary>
        /// Timestamp when this record was last modified (UTC).
        /// Automatically updated by the framework on record updates.
        /// </summary>
        [UIReadOnly]
        [UIDisplay(Name = "Modified On", ShowInList = true, ShowInDetail = false, Order = 9020)]
        public virtual DateTime? ModifiedOn { get; set; }

        /// <summary>
        /// User ID of the user who last modified this record.
        /// Automatically updated by the framework on record updates.
        /// </summary>
        [UIReadOnly]
        public virtual Guid? ModifiedById { get; set; }
        
        /// <summary>
        /// Display name of the user who last modified this record.
        /// Includes impersonation info if modified during impersonation session.
        /// Automatically updated by the framework on record updates.
        /// </summary>
        [UIReadOnly]
        [UIDisplay(Name = "Modified By", ShowInList = true, ShowInDetail = false, Order = 9030)]
        public virtual string? ModifiedByDisplayName { get; set; }

        /// <summary>
        /// Indicates whether this is an invariant (system-locked) record.
        /// Invariant records are upserted on every startup and cannot be deleted through the UI.
        /// Once set to true, this property cannot be changed back to false.
        /// </summary>
        [Column(TypeName = "bit")]
        [UIReadOnly]
        public bool IsInvariant
        {
            get => field;
            set
            {
                if (field && !value) throw new InvalidOperationException($"{nameof(IsInvariant)} is immutable once set to true.");
                field = value;
            }
        }

    }
}
