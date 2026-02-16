using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Data;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Defines CRUD permissions for a specific Role on a specific Model type.
    /// Permissions control Create access (boolean) and Read/Update/Delete access levels (None, Own, Group, All).
    /// Composite primary key on (RoleId, RecordId).
    /// </summary>
    [AuthorizeAs("Role")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(RoleId), nameof(RecordId))]
    [UIReadOnly]
    public class Permission : TipsyBaboonModel
    {
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }

        [ForeignKey("ModelRecord")]
        public Guid RecordId { get; set; }

        [UIDisplay(Name = "Use Pages", Order = 5, ShowInList = false, ShowInDetail = true)]
        public bool UsePages { get; set; }

        [UIDisplay(Name = "Can Create", Order = 10, ShowInList = true, ShowInDetail = true)]
        public bool CanCreate { get; set; }

        [UIDisplay(Name = "Read Level", Order = 20, ShowInList = true, ShowInDetail = true)]
        public PermissionLevel ReadLevel { get; set; }

        [UIDisplay(Name = "Update Level", Order = 30, ShowInList = true, ShowInDetail = true)]
        public PermissionLevel UpdateLevel { get; set; }

        [UIDisplay(Name = "Delete Level", Order = 40, ShowInList = true, ShowInDetail = true)]
        public PermissionLevel DeleteLevel { get; set; }

        [GenerateInvariantRecords]
        public static IEnumerable<Permission> GetInvariantRecords => new List<Permission>
        {
            // User Admin has full CRUD access to User and Role tables
            new Permission
            {
                RoleId = Role.UserAdminRoleId,
                RecordId = ModelRegistry.GenerateDeterministicId("Governance|User"),
                UsePages = true,
                CanCreate = true,
                ReadLevel = PermissionLevel.All,
                UpdateLevel = PermissionLevel.All,
                DeleteLevel = PermissionLevel.All,
                IsInvariant = true,
                OwnershipLevel = OwnershipLevel.Public,
                CreatedByDisplayName = "System"
            },
            new Permission
            {
                RoleId = Role.UserAdminRoleId,
                RecordId = ModelRegistry.GenerateDeterministicId("Governance|Role"),
                UsePages = true,
                CanCreate = true,
                ReadLevel = PermissionLevel.All,
                UpdateLevel = PermissionLevel.All,
                DeleteLevel = PermissionLevel.All,
                IsInvariant = true,
                OwnershipLevel = OwnershipLevel.Public,
                CreatedByDisplayName = "System"
            },
            // User Admin has read-only access to RADModule and ModelRecord tables
            new Permission
            {
                RoleId = Role.UserAdminRoleId,
                RecordId = ModelRegistry.GenerateDeterministicId("Governance|RADModule"),
                UsePages = true,
                CanCreate = false,
                ReadLevel = PermissionLevel.All,
                UpdateLevel = PermissionLevel.None,
                DeleteLevel = PermissionLevel.None,
                IsInvariant = true,
                OwnershipLevel = OwnershipLevel.Public,
                CreatedByDisplayName = "System"
            },
            new Permission
            {
                RoleId = Role.UserAdminRoleId,
                RecordId = ModelRegistry.GenerateDeterministicId("Governance|ModelRecord"),
                UsePages = true,
                CanCreate = false,
                ReadLevel = PermissionLevel.All,
                UpdateLevel = PermissionLevel.None,
                DeleteLevel = PermissionLevel.None,
                IsInvariant = true,
                OwnershipLevel = OwnershipLevel.Public,
                CreatedByDisplayName = "System"
            }
        };
    }
}
