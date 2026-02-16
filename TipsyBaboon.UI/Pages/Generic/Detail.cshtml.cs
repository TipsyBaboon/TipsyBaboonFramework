using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Extensions;

namespace TipsyBaboon.UI.Pages.Generic
{
    /// <summary>
    /// Razor Page model for the generic entity detail/edit view.
    /// Loads a single record by ID, applies layout customizations, evaluates RBAC and ownership,
    /// and renders the standard detail form. Supports modal and full-page modes.
    /// Redirects to a module-specific page if one exists on disk.
    /// </summary>
    public class DetailModel : PageModel
    {
        private readonly BaseModelStore _store;
        private readonly IWebHostEnvironment _env;
        private readonly GenericPageOptions _options;
        private readonly IPermissionService _permissionService;
        private readonly ILayoutService _layoutService;

        public DetailModel(
            BaseModelStore store,
            IWebHostEnvironment env,
            GenericPageOptions options,
            IPermissionService permissionService,
            ILayoutService layoutService)
        {
            _store = store;
            _env = env;
            _options = options;
            _permissionService = permissionService;
            _layoutService = layoutService;
        }

        public string Module { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public Type? EntityType { get; set; }

        public object? Entity { get; set; }


        public SchemaDefinition? Schema
        {
            get
            {
                if (field == null && !string.IsNullOrEmpty(TypeName) && !string.IsNullOrEmpty(Module))
                    field = ModelRegistry.GetModel(TypeName, Module);

                return field;
            }
        }

        public string EntityId { get; set; } = string.Empty;

        public bool IsModal { get; set; }

        public bool AutoSave => _options.AutoSave;

        public string ApiPrefix { get; set; } = string.Empty;

        public string FormId { get; set; } = Guid.NewGuid().ToString();

        public string JsPrefix { get; set; } = "/_tipsybaboon/js";

        public string LayoutPage => _options.LayoutPage;

        public string[] ModelScriptPaths => Schema?.ScriptPaths ?? [];
        public string[] ModelStylePaths => Schema?.StylePaths ?? [];

        public bool HasOwnership { get; set; }
        public List<OwnershipLevel> OwnershipLevels { get; set; } = new();
        public bool IsOwner { get; set; }
        public Guid? CurrentUserId { get; set; }
        public List<UserGroupInfo> UserGroups { get; set; } = new();
        public string? OwnerDisplayName { get; set; }
        public string? CurrentGroupName { get; set; }

        public string? RecordName
        {
            get
            {
                if (Entity == null || Schema == null || string.IsNullOrEmpty(Schema.RecordNameProperty))
                    return null;

                var property = EntityType?.GetProperty(Schema.RecordNameProperty);
                if (property == null)
                    return null;

                var value = property.GetValue(Entity);
                return value?.ToString();
            }
        }

        public async Task<IActionResult> OnGetAsync(string module, string type, string id)
        {
            Module = module;
            TypeName = type;
            EntityId = id;

            IsModal = Request.Query.ContainsKey("isModal") || Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            var customPagePath = $"Pages/{module}/{type}/Detail.cshtml";
            var fullPath = Path.Combine(_env.ContentRootPath, customPagePath);
            if (System.IO.File.Exists(fullPath))
            {
                return RedirectToPage($"/{module}/{type}/Detail", new { id });
            }

            if (Schema == null)
            {
                return NotFound($"Type '{type}' not found in registered models for module '{module}'");
            }

            if (!await _permissionService.HasPageAccessForSchemaAsync(Schema))
            {
                return StatusCode(403);
            }

            if (!await _permissionService.HasPermissionForSchemaAsync(Schema, ActionType.Read))
            {
                return StatusCode(403);
            }

            if (IsModal)
            {
                foreach (var section in Schema.Layout)
                {
                    section.IsInitiallyCollapsed = false;
                }
            }

            EntityType = Schema.ModelType;
            if (EntityType == null)
            {
                return NotFound($"EntityType not available for '{Schema.FullyQualifiedTypeName}'");
            }

            Entity = await _store.GetByIdAsync(EntityType, id);

            if (Entity == null)
            {
                return NotFound($"{type} with ID '{id}' not found");
            }

            var entityModel = Entity as TipsyBaboonModel;
            if (entityModel != null && !await _permissionService.CanAccessRecordForSchemaAsync(entityModel, Schema, ActionType.Read))
            {
                return NotFound($"{type} with ID '{id}' not found");
            }

            var hasUpdatePermission = await _permissionService.HasPermissionForSchemaAsync(Schema, ActionType.Update);
            var isInvariant = Entity.IsInvariantRecord();

            if (!hasUpdatePermission || isInvariant)
            {
                Schema.IsReadOnly = true;
                Schema.IsUIReadOnly = true;
                foreach (var column in Schema.Columns.Where(c => !c.IsNavigationProperty))
                {
                    column.IsReadOnly = true;
                    column.IsUIReadOnly = true;
                }
            }

            if (Schema.ModuleName == "Governance" && Schema.TypeName == "RolePermission")
            {
                var rpEntity = Entity as RolePermission;
                if (rpEntity != null)
                {
                    var targetModel = ModelRegistry.GetModel(rpEntity.RecordId);
                    if (targetModel != null && targetModel.IsUIReadOnly)
                    {
                        Schema.Columns.Single(c => c.ColumnName == "CanCreate").IsUIReadOnly = true;
                        Schema.Columns.Single(c => c.ColumnName == "UpdateLevel").IsUIReadOnly = true;
                        Schema.Columns.Single(c => c.ColumnName == "DeleteLevel").IsUIReadOnly = true;

                        rpEntity.CanCreate = false;
                        rpEntity.UpdateLevel = PermissionLevel.None;
                        rpEntity.DeleteLevel = PermissionLevel.None;
                    }
                }
            }

            await _permissionService.ApplyFieldPrivilegesToSchemaAsync(Schema);

            await _layoutService.ApplyLayoutToSchemaAsync(Schema, Core.LayoutType.Default);

            if (Entity == null)
            {
                return NotFound($"{type} with ID '{id}' not found");
            }

            ApiPrefix = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.ApiRoutePrefix;

            HasOwnership = Schema.HasOwnership;
            OwnershipLevels = Schema.OwnershipLevels;
            CurrentUserId = _permissionService.CurrentUserId;
            IsOwner = entityModel?.OwnerId != null && entityModel.OwnerId == CurrentUserId;

            if (HasOwnership)
            {
                if (OwnershipLevels.Contains(OwnershipLevel.Group))
                {
                    UserGroups = await _permissionService.GetCurrentUserGroupsAsync();
                }

                if (entityModel?.OwnerId != null)
                {
                    var owner = await _store.GetByIdAsync("Governance", "User", entityModel.OwnerId.ToString()!);
                    if (owner is User ownerUser)
                    {
                        OwnerDisplayName = ownerUser.DisplayName ?? ownerUser.UserName;
                    }
                }

                if (entityModel?.GroupId != null)
                {
                    var group = await _store.GetByIdAsync("Governance", "UserGroup", entityModel.GroupId.ToString()!);
                    if (group is UserGroup userGroup)
                    {
                        CurrentGroupName = userGroup.Name;
                    }
                }
            }

            return Page();
        }
    }
}
