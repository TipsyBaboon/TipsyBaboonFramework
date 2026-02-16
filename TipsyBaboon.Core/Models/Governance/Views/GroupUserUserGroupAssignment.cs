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
    [AuthorizeAs("UserGroup")]
    [ViewModel(SourceTables = new[] { "Users", "UserGroups", "UserGroupAssignments" },
               Description = "User-centric group assignment view for managing user groups")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(UserId), nameof(UserGroupId))]
    public class GroupUserUserGroupAssignment : TipsyBaboonModel, IModelWithSaveAction
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

        [UIDisplay(Name = "User Name", Order = 2)]
        public string UserName { get; set; } = string.Empty;

        [Column("UserGroupId", Order = 1)]
        [ForeignKey("UserGroup")]
        public Guid UserGroupId { get; set; }

        [RecordName]
        public string GroupName { get; set; } = string.Empty;

        public string? GroupDescription { get; set; }

        [UIDisplay(Name = "Assigned", Order = 1, ShowInList = true)]
        public bool IsAssigned { get; set; }

        public UserGroup? UserGroup { get; set; }
        public User? User { get; set; }

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Update)
                return SaveResponseType.Continue;

            try
            {
                var repository = serviceProvider.GetService<BaseModelStore>()
                    ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

                var request = PagedRequest.Create(page: 1, pageSize: 1)
                    .WithFilter(FilterCriteria.Equals("UserId", UserId))
                    .WithFilter(FilterCriteria.Equals("UserGroupId", UserGroupId));
                var result = await repository.QueryTypedAsync<UserGroupAssignment>(request, cancellationToken);
                var existingAssignment = result.Items.FirstOrDefault();

                if (IsAssigned && existingAssignment == null)
                {
                    var newAssignment = new UserGroupAssignment
                    {
                        UserId = UserId,
                        UserGroupId = UserGroupId,
                    };
                    await repository.InsertTypedAsync<UserGroupAssignment>(newAssignment, cancellationToken);
                }
                else if (!IsAssigned && existingAssignment != null)
                {
                    await repository.DeleteTypedAsync<UserGroupAssignment>(new Dictionary<string, object> { ["UserId"] = existingAssignment.UserId, ["UserGroupId"] = existingAssignment.UserGroupId }, cancellationToken);
                }

                return SaveResponseType.ChangeApplied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GroupUserUserGroupAssignment.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "User", Alias = "u" },
                new() { TableName = "UserGroup", Alias = "g", JoinType = JoinType.Cross },
                new() { TableName = "UserGroupAssignment", Alias = "uga", JoinType = JoinType.Left }
            },
            Joins = new()
            {
                new() { LeftTable = "uga", LeftColumn = "UserId", RightTable = "u", RightColumn = "Id" },
                new() { LeftTable = "uga", LeftColumn = "UserGroupId", RightTable = "g", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "u.Id", TargetProperty = "UserId" },

                new() { SourceExpression = "ISNULL(uga.CreatedOn, u.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(uga.CreatedByDisplayName, u.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(uga.CreatedById, u.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(uga.ModifiedOn, u.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(uga.ModifiedByDisplayName, u.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(uga.ModifiedById, u.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(uga.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(uga.OwnershipLevel, u.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(uga.OwnerId, u.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(uga.GroupId, u.GroupId)", TargetProperty = "GroupId" },

                new() { SourceExpression = "u.UserName", TargetProperty = "UserName" },
                new() { SourceExpression = "g.Id", TargetProperty = "UserGroupId" },
                new() { SourceExpression = "g.Name", TargetProperty = "GroupName" },
                new() { SourceExpression = "g.Description", TargetProperty = "GroupDescription" },
                new() { SourceExpression = "CAST(CASE WHEN uga.UserId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            }
        };
    }
}
