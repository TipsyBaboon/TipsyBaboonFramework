using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;
using LayoutModel = TipsyBaboon.Core.Models.Governance.Layout;

namespace TipsyBaboon.UI.Pages.Governance.Layout
{
    [Authorize]
    /// <summary>
    /// Razor Page model for the Layout detail/editor view.
    /// Loads a saved layout definition and its target schema metadata,
    /// enabling administrators to customize form/grid layouts per entity type.
    /// </summary>
    public class DetailModel : PageModel
    {
        private readonly IPermissionService _permissionService;
        private readonly BaseModelStore _store;

        public DetailModel(IPermissionService permissionService, BaseModelStore store)
        {
            _permissionService = permissionService;
            _store = store;
        }

        public Guid LayoutId { get; set; }
        public string LayoutName { get; set; } = string.Empty;
        public LayoutType LayoutType { get; set; }
        public string Module { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string BackUrl { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
        {
            var layoutSchema = ModelRegistry.GetModel("Layout", "Governance");

            var layout = await _store.GetTypedByIdAsync<LayoutModel>(id, cancellationToken);
            if (layoutSchema == null || !await _permissionService.HasPermissionAsync(layoutSchema.RecordModelId, ActionType.Update))
            {
                return StatusCode(403);
            }
            if (layout == null)
            {
                return NotFound($"Layout '{id}' not found");
            }

            var modelRecord = await _store.GetTypedByIdAsync<ModelRecord>(layout.ModelRecordId, cancellationToken);
            if (modelRecord == null)
            {
                return NotFound($"Model record for layout not found");
            }

            var radModule = await _store.GetTypedByIdAsync<RADModule>(modelRecord.ModuleId, cancellationToken);
            var moduleName = radModule?.Name ?? "Unknown";

            var schema = ModelRegistry.GetModel(modelRecord.Id);

            LayoutId = layout.Id;
            LayoutType = layout.LayoutType;
            LayoutName = layout.LayoutType.ToString();
            Module = moduleName;
            TypeName = modelRecord.Name;
            DisplayName = schema?.MenuLabel ?? modelRecord.Name;
            ApiUrl = $"/api/layout/{LayoutId}";
            BackUrl = $"/Governance/Model/Detail/{modelRecord.Id}";

            return Page();
        }
    }
}
