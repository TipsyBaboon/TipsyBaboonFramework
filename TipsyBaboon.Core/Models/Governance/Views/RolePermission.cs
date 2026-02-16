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
    [ViewModel(SourceTables = new[] { "Role", "ModelRecord", "Permission" },
               Description = "Role permission view for managing model access levels")]
    [UIEntity("Role Permission", ModuleName = "Governance", ShowInMenu = false, DefaultSort = ["ModuleName|ASC", "ModelName|ASC"])]
    [PrimaryKey(nameof(RoleId), nameof(RecordId))]
    public class RolePermission : TipsyBaboonModel, IModelWithSaveAction
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

        [ForeignKey("ModelRecord")]
        public Guid RecordId { get; set; }

        [UIDisplay(Name = "Module", Order = 1, ShowInList = true)]
        public string ModuleName { get; set; } = string.Empty;

        [UIDisplay(Name = "Model", Order = 2, ShowInList = true)]
        public string ModelName { get; set; } = string.Empty;

        public string TableName { get; set; } = string.Empty;

        [UIDisplay(Name = "Use Pages", Order = 9, ShowInList = false, ShowInDetail = true)]
        public bool UsePages { get; set; } = false;

        [UIDisplay(Name = "Create", Order = 10, ShowInList = true)]
        public bool CanCreate { get; set; } = false;

        [UIDisplay(Name = "Read", Order = 11, ShowInList = true)]
        public PermissionLevel ReadLevel { get; set; } = PermissionLevel.None;

        [UIDisplay(Name = "Update", Order = 12, ShowInList = true)]
        public PermissionLevel UpdateLevel { get; set; } = PermissionLevel.None;

        [UIDisplay(Name = "Delete", Order = 13, ShowInList = true)]
        public PermissionLevel DeleteLevel { get; set; } = PermissionLevel.None;

        [NotMapped]
        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public bool IsTargetModelReadOnly { get; set; } = false;

        public ModelRecord? ModelRecord { get; set; }

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action != ActionType.Update)
                return SaveResponseType.Continue;

            if (UpdateLevel > ReadLevel)
                throw new InvalidOperationException("UpdateLevel cannot exceed ReadLevel");
            if (DeleteLevel > ReadLevel)
                throw new InvalidOperationException("DeleteLevel cannot exceed ReadLevel");

            try
            {
                var permissionRepository = serviceProvider.GetService<BaseModelStore>()
                    ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

                var request = PagedRequest.Create(page: 1, pageSize: 1)
                    .WithFilter(FilterCriteria.Equals("RoleId", RoleId))
                    .WithFilter(FilterCriteria.Equals("RecordId", RecordId));
                var result = await permissionRepository.QueryTypedAsync<Permission>(request, cancellationToken);
                var existingPermission = result.Items.FirstOrDefault();

                if (existingPermission == null)
                {
                    var newPermission = new Permission
                    {
                        RoleId = RoleId,
                        RecordId = RecordId,
                        UsePages = UsePages,
                        CanCreate = CanCreate,
                        ReadLevel = ReadLevel,
                        UpdateLevel = UpdateLevel,
                        DeleteLevel = DeleteLevel
                    };
                    await permissionRepository.InsertTypedAsync<Permission>(newPermission, cancellationToken);
                }
                else
                {
                    existingPermission.UsePages = UsePages;
                    existingPermission.CanCreate = CanCreate;
                    existingPermission.ReadLevel = ReadLevel;
                    existingPermission.UpdateLevel = UpdateLevel;
                    existingPermission.DeleteLevel = DeleteLevel;

                    await permissionRepository.UpdateTypedAsync<Permission>(existingPermission, cancellationToken);
                }

                return SaveResponseType.ChangeApplied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RolePermission.BeforeChangeAsync error: {ex}");
                throw;
            }
        }

        public static ViewDefinition ViewDefinition => new()
        {
            Tables = new()
            {
                new() { TableName = "ModelRecord", Alias = "m" },
                new() { TableName = "RADModule", Alias = "mod", JoinType = JoinType.Inner },
                new() { TableName = "Role", Alias = "r", JoinType = JoinType.Cross },
                new() { TableName = "Permission", Alias = "p", JoinType = JoinType.Left }
            },
            Joins = new()
            {
                new() { LeftTable = "m", LeftColumn = "ModuleId", RightTable = "mod", RightColumn = "Id" },
                new() { LeftTable = "p", LeftColumn = "RoleId", RightTable = "r", RightColumn = "Id" },
                new() { LeftTable = "p", LeftColumn = "RecordId", RightTable = "m", RightColumn = "Id" }
            },
            Columns = new()
            {
                new() { SourceExpression = "r.Id", TargetProperty = "RoleId" },

                new() { SourceExpression = "ISNULL(p.CreatedOn, r.CreatedOn)", TargetProperty = "CreatedOn" },
                new() { SourceExpression = "ISNULL(p.CreatedByDisplayName, r.CreatedByDisplayName)", TargetProperty = "CreatedByDisplayName" },
                new() { SourceExpression = "ISNULL(p.CreatedById, r.CreatedById)", TargetProperty = "CreatedById" },
                new() { SourceExpression = "ISNULL(p.ModifiedOn, r.ModifiedOn)", TargetProperty = "ModifiedOn" },
                new() { SourceExpression = "ISNULL(p.ModifiedByDisplayName, r.ModifiedByDisplayName)", TargetProperty = "ModifiedByDisplayName" },
                new() { SourceExpression = "ISNULL(p.ModifiedById, r.ModifiedById)", TargetProperty = "ModifiedById" },
                new() { SourceExpression = "ISNULL(p.IsInvariant, 0)", TargetProperty = "IsInvariant" },
                new() { SourceExpression = "ISNULL(p.OwnershipLevel, r.OwnershipLevel)", TargetProperty = "OwnershipLevel" },
                new() { SourceExpression = "ISNULL(p.OwnerId, r.OwnerId)", TargetProperty = "OwnerId" },
                new() { SourceExpression = "ISNULL(p.GroupId, r.GroupId)", TargetProperty = "GroupId" },

                new() { SourceExpression = "r.Name", TargetProperty = "RoleName" },
                new() { SourceExpression = "r.Description", TargetProperty = "RoleDescription" },
                new() { SourceExpression = "m.Id", TargetProperty = "RecordId" },
                new() { SourceExpression = "mod.Name", TargetProperty = "ModuleName" },
                new() { SourceExpression = "m.Name", TargetProperty = "ModelName" },
                new() { SourceExpression = "m.TableName", TargetProperty = "TableName" },
                new() { SourceExpression = $"CAST(COALESCE(p.UsePages, CASE WHEN r.Id = '{Role.AnonymousRoleId}' THEN 0 ELSE 1 END) AS BIT)", TargetProperty = "UsePages" },
                new() { SourceExpression = "CAST(COALESCE(p.CanCreate, 0) AS BIT)", TargetProperty = "CanCreate" },
                new() { SourceExpression = "COALESCE(p.ReadLevel, 0)", TargetProperty = "ReadLevel" },
                new() { SourceExpression = "COALESCE(p.UpdateLevel, 0)", TargetProperty = "UpdateLevel" },
                new() { SourceExpression = "COALESCE(p.DeleteLevel, 0)", TargetProperty = "DeleteLevel" }
            },
            WhereClause = $"m.HasLocalPermissions = 1"
        };
    }
}
