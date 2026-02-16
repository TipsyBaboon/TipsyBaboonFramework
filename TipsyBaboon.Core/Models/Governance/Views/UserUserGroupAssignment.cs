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
    [ViewModel(SourceTables = new[] { "UserGroups", "Users", "UserGroupAssignments" },
               Description = "Group-centric user assignment view for managing group users")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(UserGroupId), nameof(UserId))]
    public class UserUserGroupAssignment : TipsyBaboonModel, IModelWithSaveAction
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

        [Column("UserGroupId", Order = 0)]
        [ForeignKey("UserGroup")]
        public Guid UserGroupId { get; set; }

        [UIDisplay(Name = "Group Name", Order = 2, ShowInList = true)]
        [RecordName]
        public string GroupName { get; set; } = string.Empty;

        [UIDisplay(Name = "Description", Order = 3, ShowInList = false, ShowInDetail = true)]
        public string? GroupDescription { get; set; }

        [ForeignKey("User")]
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        [UIDisplay(Name = "Assigned", Order = 1, ShowInList = true)]
        public bool IsAssigned { get; set; }

        public User? User { get; set; }
        public UserGroup? UserGroup { get; set; }

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
                System.Diagnostics.Debug.WriteLine($"UserUserGroupAssignment.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "UserGroup", Alias = "g" },
                new() { TableName = "User", Alias = "u" , JoinType = JoinType.Cross},
                new() { TableName = "UserGroupAssignment", Alias = "uga", JoinType = JoinType.Left },
            },
            Joins = new()
            {
                new() { LeftTable = "uga", LeftColumn = "UserGroupId", RightTable = "g", RightColumn = "Id" },
                new() { LeftTable = "uga", LeftColumn = "UserId", RightTable = "u", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "g.Id", TargetProperty = "UserGroupId" },

                new() { SourceExpression = "ISNULL(uga.CreatedOn, g.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(uga.CreatedByDisplayName, g.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(uga.CreatedById, g.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(uga.ModifiedOn, g.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(uga.ModifiedByDisplayName, g.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(uga.ModifiedById, g.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(uga.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(uga.OwnershipLevel, g.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(uga.OwnerId, g.OwnerId)", TargetProperty = "OwnerId" },                new() { SourceExpression = "ISNULL(uga.GroupId, g.GroupId)", TargetProperty = "GroupId" },
                new() { SourceExpression = "g.Name", TargetProperty = "GroupName" },
                new() { SourceExpression = "g.Description", TargetProperty = "GroupDescription" },
                new() { SourceExpression = "u.Id", TargetProperty = "UserId" },
                new() { SourceExpression = "u.UserName", TargetProperty = "UserName" },
                new() { SourceExpression = "CAST(CASE WHEN uga.UserId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            }
        };
    }
}
