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
    [ViewModel(SourceTables = new[] { "ExternalLinks", "Roles", "RoleExternalLinks" },
               Description = "Role-centric external link assignment view for managing role's external links")]
    [ModuleName("Governance")]
    [PrimaryKey(nameof(RoleId), nameof(ExternalLinkId))]
    public class RoleExternalLinkAssignment : TipsyBaboonModel, IModelWithSaveAction
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
        public string RoleName { get; set; } = string.Empty;

        public string? RoleDescription { get; set; }

        [ForeignKey("ExternalLink")]
        public Guid ExternalLinkId { get; set; }

        [UIDisplay(Name = "Link Title", Order = 2)]
        public string ExternalLinkTitle { get; set; } = string.Empty;

        [UIDisplay(Name = "URL", Order = 3)]
        public string? ExternalLinkUrl { get; set; }

        [UIDisplay(Name = "Assigned", Order = 1)]
        public bool IsAssigned { get; set; }

        public ExternalLink? ExternalLink { get; set; }

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Update)
                return SaveResponseType.Continue;

            try
            {
                var roleExternalLinkRepository = serviceProvider.GetService<BaseModelStore>()
                    ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

                var request = PagedRequest.Create(page: 1, pageSize: 1)
                    .WithFilter(FilterCriteria.Equals("RoleId", RoleId))
                    .WithFilter(FilterCriteria.Equals("ExternalLinkId", ExternalLinkId));
                var result = await roleExternalLinkRepository.QueryTypedAsync<RoleExternalLink>(request, cancellationToken);
                var existingRoleExternalLink = result.Items.FirstOrDefault();

                if (IsAssigned && existingRoleExternalLink == null)
                {
                    var newRoleExternalLink = new RoleExternalLink
                    {
                        RoleId = RoleId,
                        ExternalLinkId = ExternalLinkId,
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
                System.Diagnostics.Debug.WriteLine($"RoleExternalLinkAssignment.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "Role", Alias = "r" },
                new() { TableName = "ExternalLink", Alias = "el", JoinType = JoinType.Cross },
                new() { TableName = "RoleExternalLink", Alias = "rel", JoinType = JoinType.Left }
            },
            Joins = new()
            {
                new() { LeftTable = "rel", LeftColumn = "ExternalLinkId", RightTable = "el", RightColumn = "Id" },
                new() { LeftTable = "rel", LeftColumn = "RoleId", RightTable = "r", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "r.Id", TargetProperty = "RoleId" },

                new() { SourceExpression = "ISNULL(rel.CreatedOn, r.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(rel.CreatedByDisplayName, r.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(rel.CreatedById, r.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(rel.ModifiedOn, r.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(rel.ModifiedByDisplayName, r.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(rel.ModifiedById, r.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(rel.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(rel.OwnershipLevel, r.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(rel.OwnerId, r.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(rel.GroupId, r.GroupId)", TargetProperty = "GroupId" },

                new() { SourceExpression = "r.Name", TargetProperty = "RoleName" },
                new() { SourceExpression = "r.Description", TargetProperty = "RoleDescription" },
                new() { SourceExpression = "el.Id", TargetProperty = "ExternalLinkId" },
                new() { SourceExpression = "el.Title", TargetProperty = "ExternalLinkTitle" },
                new() { SourceExpression = "el.Url", TargetProperty = "ExternalLinkUrl" },
                new() { SourceExpression = "CAST(CASE WHEN rel.RoleId IS NOT NULL THEN 1 ELSE 0 END AS BIT)", TargetProperty = "IsAssigned" }
            },
            WhereClause = $"r.Id NOT IN ('{Role.AnonymousRoleId}', '{Role.BasicRightsRoleId}')"
        };
    }
}
