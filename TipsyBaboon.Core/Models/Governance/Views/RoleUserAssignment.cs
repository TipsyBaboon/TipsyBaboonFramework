using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Services;
using TipsyBaboon.Core.FrameworkBase;

namespace TipsyBaboon.Core.Models.Governance
{
    [AuthorizeAs("Role")]
    [ViewModel(SourceTables = new[] { "Users", "Roles", "UserRoles" },
               Description = "User-centric role assignment view for managing user roles")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(UserId), nameof(RoleId))]
    public class RoleUserAssignment : TipsyBaboonModel, IModelWithSaveAction
    {
        // Audit fields hidden from grid for join views
        [UIDisplay(ShowInList = false)]
        public override DateTime CreatedOn { get; set; }

        [UIDisplay(ShowInList = false)]
        public override string? CreatedByDisplayName { get; set; }

        [UIDisplay(ShowInList = false)]
        public override DateTime? ModifiedOn { get; set; }

        [UIDisplay(ShowInList = false)]
        public override string? ModifiedByDisplayName { get; set; }

        [Column("UserId", Order = 0)]
        [ForeignKey("User")]
        public Guid UserId { get; set; }

        [RecordName]
        [UIDisplay(Name = "User Name", Order = 2, ShowInList = true)]
        public string UserName { get; set; } = string.Empty;

        [Column("RoleId", Order = 1)]
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public string? RoleDescription { get; set; }

        [UIDisplay(Name = "Assigned", Order = 1, ShowInList = true)]
        public bool IsAssigned { get; set; }

        public Role? Role { get; set; }


        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Update)
                return SaveResponseType.Continue; // Let Create/Delete proceed normally

            try
            {
                var userRoleRepository = serviceProvider.GetService<BaseModelStore>()
                    ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

                var request = PagedRequest.Create(page: 1, pageSize: 1)
                    .WithFilter(FilterCriteria.Equals("UserId", UserId))
                    .WithFilter(FilterCriteria.Equals("RoleId", RoleId));
                var result = await userRoleRepository.QueryTypedAsync<UserRole>(request, cancellationToken);
                var existingUserRole = result.Items.FirstOrDefault();

                if (IsAssigned && existingUserRole == null)
                {
                    var newUserRole = new UserRole
                    {
                        UserId = UserId,
                        RoleId = RoleId,
                    };
                    await userRoleRepository.InsertTypedAsync<UserRole>(newUserRole, cancellationToken);
                }
                else if (!IsAssigned && existingUserRole != null)
                {
                    await userRoleRepository.DeleteTypedAsync<UserRole>(new Dictionary<string, object> { ["UserId"] = existingUserRole.UserId, ["RoleId"] = existingUserRole.RoleId }, cancellationToken);
                }

                return SaveResponseType.ChangeApplied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RoleUserAssignment.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "User", Alias = "u" },
                new() { TableName = "Role", Alias = "r", JoinType = JoinType.Cross },
                new() { TableName = "UserRole", Alias = "ur", JoinType = JoinType.Left }
            },
            Joins = new()
            {
                new() { LeftTable = "ur", LeftColumn = "UserId", RightTable = "u", RightColumn = "Id" },
                new() { LeftTable = "ur", LeftColumn = "RoleId", RightTable = "r", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "u.Id", TargetProperty = "UserId" },

                new() { SourceExpression = "ISNULL(ur.CreatedOn, u.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(ur.CreatedByDisplayName, u.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(ur.CreatedById, u.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(ur.ModifiedOn, u.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(ur.ModifiedByDisplayName, u.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(ur.ModifiedById, u.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(ur.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(ur.OwnershipLevel, u.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(ur.OwnerId, u.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(ur.GroupId, u.GroupId)", TargetProperty = "GroupId" },

                new() { SourceExpression = "u.UserName", TargetProperty = "UserName" },
                new() { SourceExpression = "r.Id", TargetProperty = "RoleId" },
                new() { SourceExpression = "r.Name", TargetProperty = "RoleName" },
                new() { SourceExpression = "r.Description", TargetProperty = "RoleDescription" },
                new() { SourceExpression = "CAST(CASE WHEN ur.UserId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            },
            WhereClause = $"r.Id NOT IN ('{Role.AnonymousRoleId}', '{Role.BasicRightsRoleId}')"
        };
    }
}
