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
    /// <summary>
    /// View model for managing role privileges with model context.
    /// Joins Role, RolePrivilege (junction), Privilege, and ModelRecord to show which privileges
    /// are assigned to each role, grouped by model.
    /// </summary>
    [AuthorizeAs("Role")]
    [ViewModel(SourceTables = new[] { "Role", "RolePrivilege", "Privilege", "ModelRecord" },
               Description = "Role privilege assignment view for managing role privileges per model")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(RoleId), nameof(PrivilegeId))]
    public class RolePrivilegeView : TipsyBaboonModel, IModelWithSaveAction
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

        [RecordName]
        [UIDisplay(Name = "Role", Order = 1, ShowInList = false, ShowInDetail = true)]
        public string RoleName { get; set; } = string.Empty;

        public string? RoleDescription { get; set; }

        [ForeignKey("Privilege")]
        public Guid PrivilegeId { get; set; }

        [UIDisplay(Name = "Privilege", Order = 3, ShowInList = true)]
        public string PrivilegeName { get; set; } = string.Empty;

        [ForeignKey("ModelRecord")]
        public Guid ModelId { get; set; }

        [UIDisplay(Name = "Model", Order = 2, ShowInList = true)]
        public string ModelName { get; set; } = string.Empty;

        [UIDisplay(Name = "Assigned", Order = 5, ShowInList = true)]
        public bool IsAssigned { get; set; }

        public Privilege? Privilege { get; set; }

        public ModelRecord? ModelRecord { get; set; }

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Update)
                return SaveResponseType.Continue;

            try
            {
                var rolePrivilegeRepository = serviceProvider.GetService<BaseModelStore>()
                    ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

                var request = PagedRequest.Create(page: 1, pageSize: 1)
                    .WithFilter(FilterCriteria.Equals("RoleId", RoleId))
                    .WithFilter(FilterCriteria.Equals("PrivilegeId", PrivilegeId));
                var result = await rolePrivilegeRepository.QueryTypedAsync<RolePrivilege>(request, cancellationToken);
                var existingRolePrivilege = result.Items.FirstOrDefault();

                if (IsAssigned && existingRolePrivilege == null)
                {
                    // Create new assignment
                    var newRolePrivilege = new RolePrivilege
                    {
                        RoleId = RoleId,
                        PrivilegeId = PrivilegeId,
                    };
                    await rolePrivilegeRepository.InsertTypedAsync<RolePrivilege>(newRolePrivilege, cancellationToken);
                }
                else if (!IsAssigned && existingRolePrivilege != null)
                {
                    // Remove assignment
                    await rolePrivilegeRepository.DeleteTypedAsync<RolePrivilege>(
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["RoleId"] = existingRolePrivilege.RoleId,
                            ["PrivilegeId"] = existingRolePrivilege.PrivilegeId
                        },
                        cancellationToken);
                }

                return SaveResponseType.ChangeApplied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RolePrivilegeView.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "Role", Alias = "r" },
                new() { TableName = "Privilege", Alias = "priv", JoinType = JoinType.Cross },
                new() { TableName = "ModelRecord", Alias = "m", JoinType = JoinType.Inner },
                new() { TableName = "RolePrivilege", Alias = "rp", JoinType = JoinType.Left }
            },
            Joins = new()
            {
                // Join Privilege to ModelRecord to get model context for each privilege
                new() { LeftTable = "priv", LeftColumn = "RecordId", RightTable = "m", RightColumn = "Id" },
                // Join ModelRecord to Module for module name
                // Left join RolePrivilege to check if assignment exists
                new() { LeftTable = "rp", LeftColumn = "RoleId", RightTable = "r", RightColumn = "Id" },
                new() { LeftTable = "rp", LeftColumn = "PrivilegeId", RightTable = "priv", RightColumn = "Id" }
            },
            Columns = new()
            {
                // Primary key columns
                new() { SourceExpression = "r.Id", TargetProperty = "RoleId" },
                new() { SourceExpression = "priv.Id", TargetProperty = "PrivilegeId" },

                // Audit fields (use existing RolePrivilege if it exists, otherwise Role defaults)
                new() { SourceExpression = "ISNULL(rp.CreatedOn, r.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(rp.CreatedByDisplayName, r.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(rp.CreatedById, r.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(rp.ModifiedOn, r.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(rp.ModifiedByDisplayName, r.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(rp.ModifiedById, r.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(rp.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(rp.OwnershipLevel, r.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(rp.OwnerId, r.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(rp.GroupId, r.GroupId)", TargetProperty = "GroupId" },

                // Display fields
                new() { SourceExpression = "r.Name", TargetProperty = "RoleName" },
                new() { SourceExpression = "r.Description", TargetProperty = "RoleDescription" },
                new() { SourceExpression = "priv.Name", TargetProperty = "PrivilegeName" },
                new() { SourceExpression = "m.Id", TargetProperty = "ModelId" },
                new() { SourceExpression = "m.Name", TargetProperty = "ModelName" },

                // IsAssigned = true if RolePrivilege record exists
                new() { SourceExpression = "CAST(CASE WHEN rp.RoleId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            },
        };
    }
}
