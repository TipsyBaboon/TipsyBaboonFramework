using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;
using LayoutModel = TipsyBaboon.Core.Models.Governance.Layout;

namespace TipsyBaboon.UI.Controllers
{
    /// <summary>
    /// API controller for retrieving and saving model detail page layouts.
    /// Layouts define the section grouping, ordering, and collapsibility of fields
    /// on detail/create pages. Supports default and create-specific layout types.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/layout")]
    public class LayoutApiController : ControllerBase
    {
        private readonly BaseModelStore _store;
        private readonly IPermissionService _permissionService;

        public LayoutApiController(BaseModelStore store, IPermissionService permissionService)
        {
            _store = store;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Retrieves a layout definition with its sections and column assignments,
        /// merged with the model's schema columns for layout editing.
        /// </summary>
        [HttpGet("{layoutId:guid}")]
        public async Task<IActionResult> GetLayout(Guid layoutId, CancellationToken cancellationToken)
        {
            var layoutSchema = ModelRegistry.GetModel("Layout", "Governance");
            if (layoutSchema == null || !await _permissionService.HasPermissionAsync(layoutSchema.RecordModelId, Core.ActionType.Read, cancellationToken))
                return StatusCode(403);

            var layout = await _store.GetTypedByIdAsync<LayoutModel>(layoutId, cancellationToken);
            if (layout == null)
                return NotFound($"Layout '{layoutId}' not found");

            var modelRecord = await _store.GetTypedByIdAsync<ModelRecord>(layout.ModelRecordId, cancellationToken);
            if (modelRecord == null)
                return NotFound($"Model record for layout not found");

            var schema = ModelRegistry.GetModel(modelRecord.Id);
            if (schema == null)
                return NotFound($"Schema for model '{modelRecord.Name}' not found");

            var radModule = await _store.GetTypedByIdAsync<RADModule>(modelRecord.ModuleId, cancellationToken);
            var moduleName = radModule?.Name ?? "Unknown";

            var existingSections = await _store.QueryTypedAsync<LayoutSection>(
                s => s.LayoutId == layoutId, cancellationToken);

            List<LayoutSectionDto> currentLayout;
            if (existingSections.Any())
            {
                currentLayout = new List<LayoutSectionDto>();
                foreach (var section in existingSections.OrderBy(s => s.Order))
                {
                    var properties = await _store.QueryTypedAsync<LayoutProperty>(
                        p => p.LayoutSectionId == section.Id, cancellationToken);

                    currentLayout.Add(new LayoutSectionDto
                    {
                        Id = section.Id,
                        Name = section.Name,
                        Title = section.Title,
                        Description = section.Description,
                        Order = section.Order,
                        ColumnSize = section.ColumnSize,
                        IsCollapsible = section.IsCollapsible,
                        IsInitiallyCollapsed = section.IsInitiallyCollapsed,
                        CssClass = section.CssClass,
                        Properties = properties.OrderBy(p => p.Order).Select(p => new LayoutPropertyDto
                        {
                            Id = p.Id,
                            PropertyName = p.PropertyName,
                            DisplayName = schema.Columns.FirstOrDefault(c => c.PropertyName == p.PropertyName)?.DisplayName ?? p.PropertyName,
                            Order = p.Order,
                            ColumnSize = p.ColumnSize,
                            ShowLabel = p.ShowLabel,
                            CssClass = p.CssClass
                        }).ToList()
                    });
                }
            }
            else
            {
                currentLayout = schema.Layout.Select(s => new LayoutSectionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Title = s.Title,
                    Description = s.Description,
                    Order = s.Order,
                    ColumnSize = s.ColumnSize,
                    IsCollapsible = s.IsCollapsible,
                    IsInitiallyCollapsed = s.IsInitiallyCollapsed,
                    CssClass = s.CssClass,
                    Properties = s.Properties.Select(p => new LayoutPropertyDto
                    {
                        Id = p.Id,
                        PropertyName = p.PropertyName,
                        DisplayName = schema.Columns.FirstOrDefault(c => c.PropertyName == p.PropertyName)?.DisplayName ?? p.PropertyName,
                        Order = p.Order,
                        ColumnSize = p.ColumnSize,
                        ShowLabel = p.ShowLabel,
                        CssClass = p.CssClass
                    }).ToList()
                }).ToList();
            }

            var usedProperties = currentLayout.SelectMany(s => s.Properties).Select(p => p.PropertyName).ToHashSet();
            var availableFields = schema.Columns
                .Where(c =>  c.HasUIDisplay && !usedProperties.Contains(c.PropertyName))
                .Select(c => new AvailableFieldDto
                {
                    PropertyName = c.PropertyName,
                    DisplayName = c.DisplayName ?? c.PropertyName,
                    GroupName = "Details",
                    ColumnSize = c.ColumnSize,
                    IsRequired = c.IsRequired,
                    ClrTypeName = c.ClrType?.Name ?? "String"
                })
                .OrderBy(f => f.GroupName)
                .ThenBy(f => f.DisplayName)
                .ToList();

            return Ok(new LayoutEditorDto
            {
                LayoutId = layoutId,
                Module = moduleName,
                TypeName = modelRecord.Name,
                DisplayName = schema.MenuLabel ?? schema.TypeName,
                Sections = currentLayout,
                AvailableFields = availableFields
            });
        }

        /// <summary>
        /// Saves a layout definition by replacing all existing sections and properties.
        /// Validates for duplicate property assignments.
        /// </summary>
        [HttpPut("{layoutId:guid}")]
        public async Task<IActionResult> SaveLayout(Guid layoutId, [FromBody] List<LayoutSectionDto> sections, CancellationToken cancellationToken)
        {
            var layoutSchema = ModelRegistry.GetModel("Layout", "Governance");
            if (layoutSchema == null || !await _permissionService.HasPermissionAsync(layoutSchema.RecordModelId, Core.ActionType.Update, cancellationToken))
                return StatusCode(403);

            var layout = await _store.GetTypedByIdAsync<LayoutModel>(layoutId, cancellationToken);
            if (layout == null)
                return NotFound($"Layout '{layoutId}' not found");

            var usedProperties = new HashSet<string>();
            foreach (var section in sections)
            {
                foreach (var prop in section.Properties)
                {
                    if (!usedProperties.Add(prop.PropertyName))
                        return BadRequest($"Duplicate property '{prop.PropertyName}' in layout");
                }
            }

            var existingSections = await _store.QueryTypedAsync<LayoutSection>(
                s => s.LayoutId == layoutId, cancellationToken);
            foreach (var existing in existingSections)
            {
                var existingProperties = await _store.QueryTypedAsync<LayoutProperty>(
                    p => p.LayoutSectionId == existing.Id, cancellationToken);
                foreach (var prop in existingProperties)
                {
                    await _store.DeleteTypedAsync<LayoutProperty>(prop.Id, cancellationToken);
                }
                await _store.DeleteTypedAsync<LayoutSection>(existing.Id, cancellationToken);
            }

            var newSections = new List<LayoutSection>();
            var sectionOrder = 0;
            foreach (var sectionDto in sections)
            {
                var section = new LayoutSection
                {
                    Id = Guid.NewGuid(),
                    LayoutId = layoutId,
                    Name = sectionDto.Name,
                    Title = sectionDto.Title,
                    Description = sectionDto.Description,
                    Order = sectionOrder++,
                    ColumnSize = sectionDto.ColumnSize,
                    IsCollapsible = sectionDto.IsCollapsible,
                    IsInitiallyCollapsed = sectionDto.IsInitiallyCollapsed,
                    CssClass = sectionDto.CssClass
                };
                await _store.InsertTypedAsync<LayoutSection>(section, cancellationToken);

                var propOrder = 0;
                foreach (var propDto in sectionDto.Properties)
                {
                    var prop = new LayoutProperty
                    {
                        Id = Guid.NewGuid(),
                        LayoutSectionId = section.Id,
                        PropertyName = propDto.PropertyName,
                        Order = propOrder++,
                        ColumnSize = propDto.ColumnSize,
                        ShowLabel = propDto.ShowLabel,
                        CssClass = propDto.CssClass
                    };
                    await _store.InsertTypedAsync<LayoutProperty>(prop, cancellationToken);
                    section.Properties.Add(prop);
                }
                newSections.Add(section);
            }

            return Ok(new { Success = true });
        }

        /// <summary>
        /// Resets a layout to the default attribute-driven configuration by removing all custom sections.
        /// </summary>
        [HttpPost("{layoutId:guid}/reset")]
        public async Task<IActionResult> ResetLayout(Guid layoutId, CancellationToken cancellationToken)
        {
            var layoutSchema = ModelRegistry.GetModel("Layout", "Governance");
            if (layoutSchema == null || !await _permissionService.HasPermissionAsync(layoutSchema.RecordModelId, Core.ActionType.Update, cancellationToken))
                return StatusCode(403);

            var layout = await _store.GetTypedByIdAsync<LayoutModel>(layoutId, cancellationToken);
            if (layout == null)
                return NotFound($"Layout '{layoutId}' not found");

            var existingSections = await _store.QueryTypedAsync<LayoutSection>(
                s => s.LayoutId == layoutId, cancellationToken);
            foreach (var existing in existingSections)
            {
                var existingProperties = await _store.QueryTypedAsync<LayoutProperty>(
                    p => p.LayoutSectionId == existing.Id, cancellationToken);
                foreach (var prop in existingProperties)
                {
                    await _store.DeleteTypedAsync<LayoutProperty>(prop.Id, cancellationToken);
                }
                await _store.DeleteTypedAsync<LayoutSection>(existing.Id, cancellationToken);
            }

            return Ok(new { Success = true });
        }
    }

    public class LayoutEditorDto
    {
        public Guid LayoutId { get; set; }
        public string Module { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<LayoutSectionDto> Sections { get; set; } = new();
        public List<AvailableFieldDto> AvailableFields { get; set; } = new();
    }

    public class LayoutSectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Order { get; set; }
        public int ColumnSize { get; set; } = 12;
        public bool IsCollapsible { get; set; }
        public bool IsInitiallyCollapsed { get; set; }
        public string? CssClass { get; set; }
        public List<LayoutPropertyDto> Properties { get; set; } = new();
    }

    public class LayoutPropertyDto
    {
        public Guid Id { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Order { get; set; }
        public int ColumnSize { get; set; } = 12;
        public bool ShowLabel { get; set; } = true;
        public string? CssClass { get; set; }
    }

    public class AvailableFieldDto
    {
        public string PropertyName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GroupName { get; set; } = "Details";
        public int ColumnSize { get; set; } = 12;
        public bool IsRequired { get; set; }
        public string ClrTypeName { get; set; } = "String";
    }
}
