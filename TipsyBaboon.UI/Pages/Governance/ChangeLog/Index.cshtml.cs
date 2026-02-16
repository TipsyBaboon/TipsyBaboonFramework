using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Extensions;
using TipsyBaboon.UI.Models;
using TipsyBaboon.UI.Services;

namespace TipsyBaboon.UI.Pages.Governance.ChangeLog
{
    [Authorize]
    /// <summary>
    /// Razor Page model for the ChangeLog audit trail view.
    /// Displays a paginated, read-only grid of change log entries for administrative review.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly BaseModelStore _store;
        private readonly IPermissionService _permissionService;
        private readonly GenericPageOptions _options;

        public IndexModel(
            BaseModelStore store,
            IPermissionService permissionService,
            GenericPageOptions options)
        {
            _store = store;
            _permissionService = permissionService;
            _options = options;
        }

        public GridRenderModel? GridModel { get; set; }
        public string FormId { get; set; } = Guid.NewGuid().ToString();
        public string PluralName { get; set; } = "Change Log";

        public SchemaDefinition? Schema
        {
            get
            {
                if (field == null)
                    field = ModelRegistry.GetModel(nameof(Core.Models.Governance.ChangeLog), "Governance");
                return field;
            }
        }

        public string[] ModelScriptPaths => Schema?.ScriptPaths ?? [];
        public string[] ModelStylePaths => Schema?.StylePaths ?? [];

        public async Task<IActionResult> OnGetAsync()
        {
            // ChangeLog is a system audit log - access controlled purely by privilege, not standard CRUD permissions
            var radModuleSchema = ModelRegistry.GetModel("RADModule", "Governance");
            if (radModuleSchema == null || !await _permissionService.HasPrivilegeAsync(radModuleSchema.RecordModelId, "View Change Log"))
            {
                return StatusCode(403);
            }

            if (Schema == null)
            {
                return NotFound("ChangeLog schema not found");
            }

            var gridOptions = new GridBuildOptions
            {
                EnableModalView = false,
                CanCreate = false,
                CanEdit = false,
                CanDelete = false,
                ShowHeader = true
            };

            GridModel = GridBuilder.Build(typeof(Core.Models.Governance.ChangeLog), Array.Empty<object>(), gridOptions);
            GridModel.FormId = FormId;
            GridModel.ApiEndpoint = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.BuildApiUrl("Governance", "ChangeLog");

            // Disable row navigation - ChangeLog is read-only
            GridModel.EnableRowNavigation = false;

            // Clear all actions - no create/edit/delete
            GridModel.RowActions.Clear();
            GridModel.ToolbarActions.Clear();

            return Page();
        }
    }
}
