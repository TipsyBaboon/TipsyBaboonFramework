using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Represents a user group for group-level ownership and permissions.
    /// Users can be assigned to groups, enabling Group-level OwnershipLevel and permission filtering.
    /// </summary>
    [ModuleName("Governance")]
    [UIGroup("General", Title = "General Information", Order = 1)]
    [UIGroup("Users", Title = "User Assignments", Order = 20)]
    [Index(nameof(Name), IsUnique = true)]
    [UIEntity("User Groups", ModuleName = "Governance", GroupName = "Governance", Order = 30, Icon = "bi bi-people", HasOwnership = false)]
    public class UserGroup : TipsyBaboonModel, IModelWithSaveAction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [RecordName]
        [Required]
        [StringLength(450)]
        [UIDisplay(Name = "Group Name", Order = 1, GroupName = "General", ShowInList = true)]
        public string Name { get; set; } = string.Empty;

        [FieldControl("textarea")]
        [UIDisplay(Name = "Description", Order = 2, GroupName = "General", ShowInList = true)]
        public string? Description { get; set; }

        [UIDisplay(Name = "Assigned Users", Order = 10, GroupName = "Users", ShowInList = false, ShowInDetail = true)]
        public List<GroupUserUserGroupAssignment> UserUserGroupAssignments { get; set; } = new List<GroupUserUserGroupAssignment>();

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Delete)
                return SaveResponseType.Continue;

            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

            var groupId = Id;
            var assignments = (await repository.QueryTypedAsync<UserGroupAssignment>(a => a.UserGroupId == groupId, cancellationToken)).ToList();
            foreach (var a in assignments)
                await repository.DeleteTypedAsync<UserGroupAssignment>(new Dictionary<string, object> { ["UserId"] = a.UserId, ["UserGroupId"] = a.UserGroupId }, cancellationToken);

            return SaveResponseType.Continue;
        }
    }
}
