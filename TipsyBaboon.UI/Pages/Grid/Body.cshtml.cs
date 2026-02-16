using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Models;

namespace TipsyBaboon.UI.Pages.Grid
{
    [Authorize]
    /// <summary>
    /// Razor Page endpoint that renders a grid body fragment (rows + paging) for AJAX-driven pagination.
    /// Called by the client-side grid to fetch a new page of data without full page reload.
    /// </summary>
    public class BodyModel : PageModel
    {
        private readonly BaseModelStore _store;
        private readonly IPermissionService _permissionService;

        public BodyModel(BaseModelStore store, IPermissionService permissionService)
        {
            _store = store;
            _permissionService = permissionService;
        }

        public GridBodyRenderModel GridModel { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? formId = null,
            [FromQuery] string? gridId = null,
            [FromQuery] bool isEditable = false,
            [FromQuery] string? fieldName = null,
            [FromQuery] string? editableColumns = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDir = null,
            CancellationToken cancellationToken = default)
        {
            var schema = ModelRegistry.GetModel(type, module);
            if (schema == null)
                return NotFound($"Model '{type}' not found");

            if (!TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.SpecifyPageAccess
                && schema.ModuleName == "Governance"
                && (schema.TypeName == "Permission" || schema.TypeName == "RolePermission"))
            {
                var usePagesColumn = schema.Columns.FirstOrDefault(c => c.PropertyName == "UsePages");
                if (usePagesColumn != null)
                {
                    usePagesColumn.ShowInList = false;
                    usePagesColumn.ShowInForm = false;
                }
            }

            var entityType = schema.ModelType;
            if (entityType == null)
                return NotFound($"Type '{schema.FullyQualifiedTypeName}' could not be loaded");

            // ChangeLog uses privilege-based access instead of CRUD permissions
            if (module == "Governance" && type == "ChangeLog")
            {
                var radModuleSchema = ModelRegistry.GetModel("RADModule", "Governance");
                if (radModuleSchema == null || !await _permissionService.HasPrivilegeAsync(radModuleSchema.RecordModelId, "View Change Log", cancellationToken))
                {
                    return StatusCode(403, "Insufficient privileges to view change log");
                }
            }
            else if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Read, cancellationToken))
            {
                return StatusCode(403, "Insufficient permissions to read this resource");
            }

            var effectiveIsEditable = isEditable;
            if (isEditable)
            {
                effectiveIsEditable = await _permissionService.HasPermissionForSchemaAsync(
                    schema, ActionType.Update, cancellationToken);
            }

            var request = new PagedRequest { Page = page, PageSize = pageSize };

            if (!string.IsNullOrEmpty(sortBy))
            {
                var sortFields = sortBy.Split(',');
                var sortDirs = (sortDir ?? "").Split(',');

                for (int i = 0; i < sortFields.Length; i++)
                {
                    var field = sortFields[i].Trim();
                    if (string.IsNullOrEmpty(field)) continue;

                    var direction = i < sortDirs.Length && sortDirs[i].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase)
                        ? SortDirection.Descending
                        : SortDirection.Ascending;

                    var property = entityType.GetProperty(field,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (property != null)
                    {
                        request.Sorts.Add(new SortCriteria { PropertyName = property.Name, Direction = direction });
                    }
                }
            }
            else
            {
                // Apply default sort when no explicit sort is provided
                // Priority: 1) DefaultSort from UIEntity attribute, 2) RecordName ASC, 3) CreatedOn DESC
                if (schema.DefaultSort != null && schema.DefaultSort.Length > 0)
                {
                    foreach (var sortSpec in schema.DefaultSort)
                    {
                        var parts = sortSpec.Split('|');
                        if (parts.Length >= 1)
                        {
                            var field = parts[0].Trim();
                            var direction = parts.Length > 1 && parts[1].Trim().Equals("DESC", StringComparison.OrdinalIgnoreCase)
                                ? SortDirection.Descending
                                : SortDirection.Ascending;

                            var property = entityType.GetProperty(field,
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                            if (property != null)
                            {
                                request.Sorts.Add(new SortCriteria { PropertyName = property.Name, Direction = direction });
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(schema.RecordNameProperty))
                {
                    var property = entityType.GetProperty(schema.RecordNameProperty,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (property != null)
                    {
                        request.Sorts.Add(new SortCriteria { PropertyName = property.Name, Direction = SortDirection.Ascending });
                    }
                }
                else
                {
                    var createdOnProperty = entityType.GetProperty("CreatedOn",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (createdOnProperty != null)
                    {
                        request.Sorts.Add(new SortCriteria { PropertyName = createdOnProperty.Name, Direction = SortDirection.Descending });
                    }
                }
            }

            var queryParams = Request.Query;
            var knownParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "page", "pageSize", "sortBy", "sortDir", "formId", "gridId",
                "isEditable", "fieldName", "editableColumns"
            };

            foreach (var param in queryParams)
            {
                if (knownParams.Contains(param.Key))
                    continue;

                var filterValue = param.Value.ToString();
                if (string.IsNullOrEmpty(filterValue))
                    continue;

                string propertyName;
                bool isColumnFilter = false;

                if (param.Key.StartsWith("filter_", StringComparison.OrdinalIgnoreCase))
                {
                    propertyName = param.Key.Substring(7);
                    isColumnFilter = true;
                }
                else
                {
                    propertyName = param.Key;
                }

                var property = entityType.GetProperty(propertyName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (property != null)
                {
                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    if (isColumnFilter && underlyingType == typeof(string))
                    {
                        request.Filters.Add(FilterCriteria.Contains(property.Name, filterValue));
                    }
                    else
                    {
                        var value = ConvertFilterValue(filterValue, property.PropertyType);
                        if (value != null)
                        {
                            request.Filters.Add(FilterCriteria.Equals(property.Name, value));
                        }
                    }
                }
            }

            var result = await _store.QueryAsync(entityType, request, cancellationToken);

            var filteredItems = await _permissionService.FilterRecordsByPermissionForSchemaAsync(
                result.Items, schema, ActionType.Read, cancellationToken);

            if (schema.ModuleName == "Governance" && schema.TypeName == "RolePermission")
            {
                foreach (var item in filteredItems.OfType<RolePermission>())
                {
                    var targetSchema = ModelRegistry.GetModel(item.RecordId);
                    item.IsTargetModelReadOnly = targetSchema?.IsUIReadOnly ?? false;
                }
            }

            var editableSet = !string.IsNullOrEmpty(editableColumns)
                ? new HashSet<string>(editableColumns.Split(','), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();

            var columns = BuildGridColumns(schema, editableSet);

            GridModel = new GridBodyRenderModel
            {
                GridId = gridId ?? Guid.NewGuid().ToString("N"),
                FormId = formId ?? Guid.NewGuid().ToString(),
                Schema = schema,
                Columns = columns,
                Rows = filteredItems,
                IsEditable = effectiveIsEditable,
                FieldName = fieldName,
                CurrentPage = page,
                TotalCount = result.TotalCount,
                PageSize = pageSize,
                EnableRowNavigation = schema.HasForms && !schema.IsViewModel
            };

            Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            Response.Headers["X-Current-Page"] = page.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();
            Response.Headers["X-Total-Pages"] = GridModel.TotalPages.ToString();
            Response.Headers["X-Enable-Row-Navigation"] = GridModel.EnableRowNavigation.ToString().ToLowerInvariant();

            return Page();
        }

        private static List<GridColumn> BuildGridColumns(SchemaDefinition schema, HashSet<string> editableColumnNames)
        {
            var columns = new List<GridColumn>();

            foreach (var keyCol in schema.KeyColumns)
            {
                columns.Add(new GridColumn
                {
                    PropertyName = keyCol.PropertyName,
                    DisplayName = keyCol.DisplayName ?? keyCol.PropertyName,
                    PropertyType = keyCol.ClrType,
                    Sortable = keyCol.ShowInList,
                    IsKey = true,
                    KeyOrdinal = keyCol.KeyOrdinal,
                    Hidden = !keyCol.ShowInList
                });
            }

            var keyPropertyNames = schema.KeyColumns.Select(k => k.PropertyName).ToHashSet();

            var displayColumns = schema.Columns
                .Where(c => c.ShowInList && !keyPropertyNames.Contains(c.PropertyName))
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.PropertyName)
                .ToList();

            foreach (var col in displayColumns)
            {
                var columnIsEditable = editableColumnNames.Contains(col.PropertyName) && !col.IsUIReadOnly;

                columns.Add(new GridColumn
                {
                    PropertyName = col.PropertyName,
                    DisplayName = col.DisplayName ?? col.PropertyName,
                    PropertyType = col.ClrType,
                    Sortable = true,
                    Alignment = GetDefaultAlignment(col.ClrType),
                    FormatterType = GetFormatterType(col.ClrType),
                    IsEditable = columnIsEditable
                });
            }

            return columns;
        }

        private static string GetDefaultAlignment(Type propertyType)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
                underlyingType == typeof(decimal) || underlyingType == typeof(double) ||
                underlyingType == typeof(float))
                return "right";

            if (underlyingType == typeof(bool))
                return "center";

            return "left";
        }

        private static string? GetFormatterType(Type propertyType)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(bool))
                return "Boolean";

            if (underlyingType == typeof(DateTime))
                return "DateTime";

            if (underlyingType == typeof(DateOnly))
                return "Date";

            return null;
        }

        private static object? ConvertFilterValue(string value, Type targetType)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(Guid))
                return Guid.TryParse(value, out var guidValue) ? guidValue : null;
            if (underlyingType == typeof(int))
                return int.TryParse(value, out var intValue) ? intValue : null;
            if (underlyingType == typeof(long))
                return long.TryParse(value, out var longValue) ? longValue : null;
            if (underlyingType == typeof(bool))
                return bool.TryParse(value, out var boolValue) ? boolValue : null;
            if (underlyingType == typeof(DateTime))
                return DateTime.TryParse(value, out var dateValue) ? dateValue : null;
            if (underlyingType == typeof(DateOnly))
                return DateOnly.TryParse(value, out var dateOnlyValue) ? dateOnlyValue : null;
            if (underlyingType == typeof(decimal))
                return decimal.TryParse(value, out var decValue) ? decValue : null;
            if (underlyingType == typeof(string))
                return value;

            return null;
        }
    }
}
