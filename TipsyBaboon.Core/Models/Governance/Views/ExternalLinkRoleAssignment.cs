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
    [AuthorizeAs("ExternalLink")]
    [ViewModel(SourceTables = new[] { "Roles", "ExternalLinks", "RoleExternalLinks" },
               Description = "ExternalLink-centric role assignment view for managing link access")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(ExternalLinkId), nameof(RoleId))]
    public class ExternalLinkRoleAssignment : TipsyBaboonModel, IModelWithSaveAction
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

        [Column("ExternalLinkId", Order = 0)]
        [ForeignKey("ExternalLink")]
        public Guid ExternalLinkId { get; set; }

        [RecordName]
        public string ExternalLinkTitle { get; set; } = string.Empty;

        public string? ExternalLinkUrl { get; set; }

        [Column("RoleId", Order = 1)]
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }

        [UIDisplay(Name = "Role Name", Order = 2, ShowInList = true)]
        public string RoleName { get; set; } = string.Empty;

        [UIDisplay(Name = "Description", Order = 3, ShowInList = false, ShowInDetail = true)]
        public string? RoleDescription { get; set; }

        [UIDisplay(Name = "Assigned", Order = 1, ShowInList = true)]
        public bool IsAssigned { get; set; }

        public Role? Role { get; set; }

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Update)
                return SaveResponseType.Continue;

            try
            {
                var roleExternalLinkRepository = serviceProvider.GetService<BaseModelStore>()
                    ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

                var request = PagedRequest.Create(page: 1, pageSize: 1)
                    .WithFilter(FilterCriteria.Equals("ExternalLinkId", ExternalLinkId))
                    .WithFilter(FilterCriteria.Equals("RoleId", RoleId));
                var result = await roleExternalLinkRepository.QueryTypedAsync<RoleExternalLink>(request, cancellationToken);
                var existingRoleExternalLink = result.Items.FirstOrDefault();

                if (IsAssigned && existingRoleExternalLink == null)
                {
                    var newRoleExternalLink = new RoleExternalLink
                    {
                        ExternalLinkId = ExternalLinkId,
                        RoleId = RoleId,
                    };
                    await roleExternalLinkRepository.InsertTypedAsync<RoleExternalLink>(newRoleExternalLink, cancellationToken);
                }
                else if (!IsAssigned && existingRoleExternalLink != null)
                {
                    await roleExternalLinkRepository.DeleteTypedAsync<RoleExternalLink>(new Dictionary<string, object> { ["RoleId"] = existingRoleExternalLink.RoleId, ["ExternalLinkId"] = existingRoleExternalLink.ExternalLinkId }, cancellationToken);
                }

                return SaveResponseType.ChangeApplied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExternalLinkRoleAssignment.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "ExternalLink", Alias = "el" },
                new() { TableName = "Role", Alias = "r", JoinType = JoinType.Cross },
                new() { TableName = "RoleExternalLink", Alias = "rel", JoinType = JoinType.Left }
            },
            Joins = new()
            {
                new() { LeftTable = "rel", LeftColumn = "RoleId", RightTable = "r", RightColumn = "Id" },
                new() { LeftTable = "rel", LeftColumn = "ExternalLinkId", RightTable = "el", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "el.Id", TargetProperty = "ExternalLinkId" },

                new() { SourceExpression = "ISNULL(rel.CreatedOn, el.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(rel.CreatedByDisplayName, el.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(rel.CreatedById, el.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(rel.ModifiedOn, el.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(rel.ModifiedByDisplayName, el.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(rel.ModifiedById, el.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(rel.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(rel.OwnershipLevel, el.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(rel.OwnerId, el.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(rel.GroupId, el.GroupId)", TargetProperty = "GroupId" },

                new() { SourceExpression = "el.Title", TargetProperty = "ExternalLinkTitle" },
                new() { SourceExpression = "el.Url", TargetProperty = "ExternalLinkUrl" },
                new() { SourceExpression = "r.Id", TargetProperty = "RoleId" },
                new() { SourceExpression = "r.Name", TargetProperty = "RoleName" },
                new() { SourceExpression = "r.Description", TargetProperty = "RoleDescription" },
                new() { SourceExpression = "CAST(CASE WHEN rel.RoleId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            },
            WhereClause = $"r.Id NOT IN ('{Role.AnonymousRoleId}', '{Role.BasicRightsRoleId}')"
        };
    }
}
