using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;
using TipsyBaboon.UI.Extensions;
using TipsyBaboon.UI.Models;
using TipsyBaboon.UI.Services;

namespace TipsyBaboon.UI.Pages.Generic
{
    /// <summary>
    /// Razor Page model for the generic entity list view.
    /// Resolves the entity type from route parameters, checks RBAC permissions,
    /// builds a <see cref="GridRenderModel"/>, and renders the standard grid page.
    /// Redirects to a module-specific page if one exists on disk.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly BaseModelStore _store;
        private readonly IWebHostEnvironment _env;
        private readonly GenericPageOptions _options;
        private readonly IConfigurationPageService _configService;
        private readonly IPermissionService _permissionService;

        public IndexModel(
            BaseModelStore store,
            IWebHostEnvironment env,
            GenericPageOptions options,
            IConfigurationPageService configService,
            IPermissionService permissionService)
        {
            _store = store;
            _env = env;
            _options = options;
            _configService = configService;
            _permissionService = permissionService;
        }
        public string PluralName { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public Type? EntityType { get; set; }

        public SchemaDefinition? Schema
        {
            get
            {
                if (field == null && !string.IsNullOrEmpty(TypeName) && !string.IsNullOrEmpty(Module))
                    field = ModelRegistry.GetModel(TypeName, Module);
                return field;
            }
        }

        public GridRenderModel? GridModel { get; set; }

        public string FormId { get; set; } = Guid.NewGuid().ToString();

        public string LayoutPage => _options.LayoutPage;

        public string[] ModelScriptPaths => Schema?.ScriptPaths ?? [];
        public string[] ModelStylePaths => Schema?.StylePaths ?? [];

        public async Task<IActionResult> OnGetAsync(string module, string type)
        {
            Module = module;
            TypeName = type;
            PluralName = Schema?.PluralName ?? type + "s";
            var customPagePath = $"Pages/{module}/{type}/Index.cshtml";
            var fullPath = Path.Combine(_env.ContentRootPath, customPagePath);
            if (System.IO.File.Exists(fullPath))
            {
                return RedirectToPage($"/{module}/{type}/Index");
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

            EntityType = Schema.ModelType;
            if (EntityType == null)
            {
                return NotFound($"EntityType not available for '{Schema.FullyQualifiedTypeName}'");
            }

            var uiConfig = await _configService.LoadConfigurationAsync<UIConfiguration>();
            var permissions = await _permissionService.GetEffectivePermissionsForSchemaAsync(Schema);

            var gridOptions = new GridBuildOptions
            {
                EnableModalView = uiConfig?.EnableModalView ?? _options.EnableModalView,
                CanCreate = permissions.CanCreate,
                CanEdit = permissions.CanUpdate,
                CanDelete = permissions.CanDelete
            };
            GridModel = GridBuilder.Build(EntityType, Array.Empty<object>(), gridOptions);
            GridModel.FormId = FormId;
            GridModel.ApiEndpoint = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.BuildApiUrl(module, type);

            return Page();
        }
    }
}
