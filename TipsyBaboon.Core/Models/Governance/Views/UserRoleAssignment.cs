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
    [AuthorizeAs("User")]
    [ViewModel(SourceTables = new[] { "Roles", "Users", "UserRoles" },
               Description = "Role-centric user assignment view for managing role users")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(RoleId), nameof(UserId))]
    public class UserRoleAssignment : TipsyBaboonModel, IModelWithSaveAction
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

        [Column("RoleId", Order = 0)]
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }

        [UIDisplay(Name = "Role Name", Order = 2, ShowInList = true)]
        [RecordName]
        public string RoleName { get; set; } = string.Empty;

        [UIDisplay(Name = "Description", Order = 3, ShowInList = false, ShowInDetail = true)]
        public string? RoleDescription { get; set; }

        [ForeignKey("User")]
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        [UIDisplay(Name = "Assigned", Order = 1, ShowInList = true)]
        public bool IsAssigned { get; set; }

        public User? User { get; set; }


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
                System.Diagnostics.Debug.WriteLine($"UserRoleAssignment.BeforeChangeAsync error: {ex}");
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
                new() { LeftTable = "ur", LeftColumn = "RoleId", RightTable = "r", RightColumn = "Id" },
                new() { LeftTable = "ur", LeftColumn = "UserId", RightTable = "u", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "r.Id", TargetProperty = "RoleId" },

                new() { SourceExpression = "ISNULL(ur.CreatedOn, r.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(ur.CreatedByDisplayName, r.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(ur.CreatedById, r.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(ur.ModifiedOn, r.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(ur.ModifiedByDisplayName, r.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(ur.ModifiedById, r.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(ur.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(ur.OwnershipLevel, r.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(ur.OwnerId, r.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(ur.GroupId, r.GroupId)", TargetProperty = "GroupId" },

                new() { SourceExpression = "r.Name", TargetProperty = "RoleName" },
                new() { SourceExpression = "r.Description", TargetProperty = "RoleDescription" },
                new() { SourceExpression = "u.Id", TargetProperty = "UserId" },
                new() { SourceExpression = "u.UserName", TargetProperty = "UserName" },
                new() { SourceExpression = "CAST(CASE WHEN ur.UserId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            },
            WhereClause = $"r.Id NOT IN ('{Role.AnonymousRoleId}', '{Role.BasicRightsRoleId}')"
        };
    }
}
