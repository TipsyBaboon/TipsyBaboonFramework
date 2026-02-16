using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;
using LayoutModel = TipsyBaboon.Core.Models.Governance.Layout;

namespace TipsyBaboon.UI.Services
{
    /// <summary>
    /// Loads saved layout definitions from the database and applies them to a <see cref="SchemaDefinition"/>,
    /// overriding the default attribute-driven section layout with custom section groupings and property ordering.
    /// </summary>
    public class LayoutService : ILayoutService
    {
        private readonly BaseModelStore _store;

        public LayoutService(BaseModelStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Applies a saved layout to the schema's <see cref="SchemaDefinition.Layout"/> section list.
        /// Falls back to <see cref="LayoutType.Default"/> if a layout-type-specific definition is not found.
        /// If no custom layout exists, the schema retains its attribute-driven defaults.
        /// </summary>
        /// <param name="schema">The schema to modify in place.</param>
        /// <param name="layoutType">The layout type to apply (Default or Create).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ApplyLayoutToSchemaAsync(SchemaDefinition schema, LayoutType layoutType, CancellationToken cancellationToken = default)
        {
            var layout = await GetLayoutAsync(schema.RecordModelId, layoutType, cancellationToken);
            if (layout == null && layoutType != LayoutType.Default)
            {
                layout = await GetLayoutAsync(schema.RecordModelId, LayoutType.Default, cancellationToken);
            }

            if (layout == null)
                return;

            var sections = await _store.QueryTypedAsync<LayoutSection>(
                s => s.LayoutId == layout.Id, cancellationToken);

            if (!sections.Any())
                return;

            var customLayout = new List<LayoutSection>();
            foreach (var section in sections.OrderBy(s => s.Order))
            {
                var properties = await _store.QueryTypedAsync<LayoutProperty>(
                    p => p.LayoutSectionId == section.Id, cancellationToken);

                var newSection = new LayoutSection
                {
                    Id = section.Id,
                    LayoutId = section.LayoutId,
                    Name = section.Name,
                    Title = section.Title,
                    Description = section.Description,
                    Order = section.Order,
                    ColumnSize = section.ColumnSize,
                    IsCollapsible = section.IsCollapsible,
                    IsInitiallyCollapsed = section.IsInitiallyCollapsed,
                    CssClass = section.CssClass,
                    Properties = properties.OrderBy(p => p.Order).Select(p => new LayoutProperty
                    {
                        Id = p.Id,
                        LayoutSectionId = p.LayoutSectionId,
                        PropertyName = p.PropertyName,
                        Order = p.Order,
                        ColumnSize = p.ColumnSize,
                        ShowLabel = p.ShowLabel,
                        CssClass = p.CssClass
                    }).ToList()
                };
                customLayout.Add(newSection);
            }

            schema.Layout = customLayout;
        }

        private async Task<LayoutModel?> GetLayoutAsync(Guid modelRecordId, LayoutType layoutType, CancellationToken cancellationToken)
        {
            var layouts = await _store.QueryTypedAsync<LayoutModel>(
                l => l.ModelRecordId == modelRecordId && l.LayoutType == layoutType, cancellationToken);
            return layouts.FirstOrDefault();
        }
    }
}
