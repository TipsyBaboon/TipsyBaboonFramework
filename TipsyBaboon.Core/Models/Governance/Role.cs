using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Represents a role in the TipsyBaboon authorization system.
    /// Roles group permissions and are assigned to users via UserRole junction records.
    /// Includes three system-invariant roles: UserAdmin, Anonymous, and BasicRights.
    /// </summary>
    [UIEntity("Role", ModuleName = "Governance", GroupName = "Governance", Order = 20, Icon = "bi bi-people-fill", HasOwnership = false)]
    [UIGroup("Basic", Title = "Role Information", Order = 10)]
    [UIGroup("Relationships", Title = "Relationships", Order = 20, Collapsible = true, InitiallyCollapsed = true)]
    [Index(nameof(Name), IsUnique = true)]
    [Index(nameof(NormalizedName), IsUnique = true)]
    public class Role : TipsyBaboonModel, IModelWithSaveAction
    {
        public static readonly Guid UserAdminRoleId = ModelRegistry.GenerateDeterministicId("Role:UserAdmin");

        public static readonly Guid AnonymousRoleId = ModelRegistry.GenerateDeterministicId("Role:Anonymous");

        public static readonly Guid BasicRightsRoleId = ModelRegistry.GenerateDeterministicId("Role:BasicRights");

        [Key]
        [UIDisplay(Order = -1, ShowInList = false, ShowInDetail = true)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [RecordName]
        [Required]
        [StringLength(256)]
        [UIDisplay(Name = "Role Name", GroupName = "Basic", Order = 10, ColumnSize = 6, ShowInList = true)]
        public string Name { get; set; } = string.Empty;

        [UIReadOnly]
        [StringLength(256)]
        [UIDisplay(Name = "Normalized Name", GroupName = "Basic", Order = 15, ShowInList = false, ShowInDetail = false)]
        public string? NormalizedName { get; set; }

        [UIDisplay(Name = "Description", GroupName = "Basic", Order = 20, ColumnSize = 12, ShowInList = true)]
        [FieldControl("TextArea")]
        public string? Description { get; set; }

        [ReadOnly(true)]
        public string? ConcurrencyStamp { get; set; }

        [UIDisplay(Name = "Users in Role", GroupName = "Relationships", Order = 10, ShowInList = false, ShowInDetail = false)]
        public List<UserRole> UserRoles { get; set; } = new List<UserRole>();

        [UIDisplay(Name = "User Assignments", GroupName = "Relationships", Order = 11, ShowInList = false)]
        public List<RoleUserAssignment> RoleUserAssignments { get; set; } = new List<RoleUserAssignment>();

        [UIDisplay(Name = "Model Permissions", GroupName = "Relationships", ColumnSize = 12, Order = 12, ShowInList = false)]
        public List<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

        [UIDisplay(Name = "Privileges", GroupName = "Relationships", Order = 13, ShowInList = false)]
        public List<RolePrivilegeView> RolePrivilegeViews { get; set; } = new List<RolePrivilegeView>();

        public List<RoleExternalLink> RoleExternalLinks { get; set; } = new List<RoleExternalLink>();

        [UIDisplay(Name = "External Links", GroupName = "Relationships", Order = 14, ShowInList = false)]
        public List<RoleExternalLinkAssignment> RoleExternalLinkAssignments { get; set; } = new List<RoleExternalLinkAssignment>();

        public List<RoleClaim> Claims { get; set; } = new List<RoleClaim>();

        public List<Permission> Permissions { get; set; } = new List<Permission>();

        [GenerateInvariantRecords]
        public static IEnumerable<Role> GetInvariantRecords { get => new List<Role>
                {
                    new Role
                    {
                        Id = UserAdminRoleId,
                        Name = "User Admin",
                        NormalizedName = "USERADMIN",
                        Description = "User administration role with full permissions to manage users and roles (system invariant)",
                        IsInvariant = true
                    },

                    new Role
                    {
                        Id = AnonymousRoleId,
                        Name = "Anonymous",
                        NormalizedName = "ANONYMOUS",
                        Description = "Role applied to unauthenticated users for public API access (system invariant)",
                        IsInvariant = true
                    },

                    new Role
                    {
                        Id = BasicRightsRoleId,
                        Name = "Basic Rights",
                        NormalizedName = "BASICRIGHTS",
                        Description = "Basic rights automatically applied to all registered users (system invariant)",
                        IsInvariant = true
                    }

                };
        }

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action == ActionType.Create || action == ActionType.Update)
            {
                NormalizedName = Name?.Replace(" ", string.Empty).ToUpperInvariant();
                ConcurrencyStamp = Guid.NewGuid().ToString();
                return SaveResponseType.Continue;
            }

            if (action != ActionType.Delete)
                return SaveResponseType.Continue;

            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

            var roleId = Id;

            var userRoles = (await repository.QueryTypedAsync<UserRole>(ur => ur.RoleId == roleId, cancellationToken)).ToList();
            foreach (var ur in userRoles)
                await repository.DeleteTypedAsync<UserRole>(new Dictionary<string, object> { ["UserId"] = ur.UserId, ["RoleId"] = ur.RoleId }, cancellationToken);

            var permissions = (await repository.QueryTypedAsync<Permission>(p => p.RoleId == roleId, cancellationToken)).ToList();
            foreach (var p in permissions)
                await repository.DeleteTypedAsync<Permission>(new Dictionary<string, object> { ["RoleId"] = p.RoleId, ["RecordId"] = p.RecordId }, cancellationToken);

            var rolePrivileges = (await repository.QueryTypedAsync<RolePrivilege>(rp => rp.RoleId == roleId, cancellationToken)).ToList();
            foreach (var rp in rolePrivileges)
                await repository.DeleteTypedAsync<RolePrivilege>(new Dictionary<string, object> { ["RoleId"] = rp.RoleId, ["PrivilegeId"] = rp.PrivilegeId }, cancellationToken);

            var roleClaimIds = (await repository.QueryTypedAsync<RoleClaim>(rc => rc.RoleId == roleId, cancellationToken))
                .Select(rc => (object)rc.Id).ToList();
            if (roleClaimIds.Count > 0)
                await repository.BulkDeleteTypedAsync<RoleClaim>(roleClaimIds, cancellationToken);

            var roleExternalLinks = (await repository.QueryTypedAsync<RoleExternalLink>(rel => rel.RoleId == roleId, cancellationToken)).ToList();
            foreach (var rel in roleExternalLinks)
                await repository.DeleteTypedAsync<RoleExternalLink>(new Dictionary<string, object> { ["RoleId"] = rel.RoleId, ["ExternalLinkId"] = rel.ExternalLinkId }, cancellationToken);

            return SaveResponseType.Continue;
        }
                
        
    }
}
