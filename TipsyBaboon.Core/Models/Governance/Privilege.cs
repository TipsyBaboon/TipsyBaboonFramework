using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Represents a custom privilege beyond standard CRUD permissions.
    /// Privileges are declared via [Privilege] attributes on models and assigned to roles via RolePrivilege.
    /// Examples: "Change Password", "Impersonate User", "Manage Configuration".
    /// </summary>
    [ModuleName("Governance")]
    [Index(nameof(RecordId), nameof(Name), IsUnique = true)]
    [UIReadOnly]
    public class Privilege : TipsyBaboonModel
    {
        public Privilege()
        {
            OwnershipLevel = OwnershipLevel.Public;
        }

        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RecordId { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;
    }
}
