using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.UI.Models;

namespace TipsyBaboon.UI.Services
{
    /// <summary>
    /// Builds <see cref="GridRenderModel"/> instances from model metadata and entity collections.
    /// Configures columns, toolbar actions, sorting, filtering, and API endpoints
    /// for the generic grid Razor partial used on list pages.
    /// </summary>
    public class GridBuilder
    {
        public GridBuilder()
        {
        }

        /// <summary>
        /// Builds a grid render model for the specified entity type and collection.
        /// Reads schema metadata to configure visible columns, action buttons,
        /// default sort order, and API endpoints for client-side operations.
        /// </summary>
        /// <param name="entityType">The CLR model type.</param>
        /// <param name="entities">The initial set of entities (may be empty for API-loaded grids).</param>
        /// <param name="options">Grid configuration options including CRUD permissions and parent context.</param>
        /// <returns>A fully configured <see cref="GridRenderModel"/>.</returns>
        public static GridRenderModel Build(Type entityType, IEnumerable<object> entities, GridBuildOptions? options = null)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options), "GridBuildOptions must be provided");

            var schema = ModelRegistry.GetModel(entityType);

            if (schema == null)
                throw new InvalidOperationException($"Schema definition not found for entity type '{entityType.Name}'");

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

            var moduleName = schema.ModuleName;

            var recordNameColumn = schema.Columns.FirstOrDefault(c => c.PropertyName == schema.RecordNameProperty);
            var displayNameColumn = recordNameColumn ?? schema.Columns.FirstOrDefault(c => !c.IsPrimaryKey);

            var entityDisplayName = displayNameColumn?.DisplayName ?? entityType.Name + "s";
            var entitySingularName = displayNameColumn?.DisplayName ?? entityType.Name;

            var gridId = options.GridId ?? (options.ParentTypeName != null
                ? $"{options.ParentTypeName}-{schema.TypeName}-grid"
                : $"{schema.TypeName}-index-grid");

            var model = new GridRenderModel
            {
                GridId = gridId,
                EntityType = entityType,
                EntityTypeName = schema.TableName,
                ModuleName = moduleName,
                EntityDisplayName = schema.MenuLabel ?? schema.TypeName,
                EntitySingularName = schema.TableName,
                Entities = entities,
                EnableModalView = options.EnableModalView,
                CreateUseModal = schema.FullScreenMode.HasFlag(FullScreenMode.Create) ? false : options.EnableModalView,
                EditUseModal = schema.FullScreenMode.HasFlag(FullScreenMode.Detail) ? false : options.EnableModalView,
                CanCreate = options.CanCreate,
                CanEdit = options.CanEdit,
                CanDelete = options.CanDelete,
                PageSize = options.PageSize,
                CurrentPage = options.CurrentPage,
                TotalCount = options.TotalCount > 0 ? options.TotalCount : entities.Count(),
                EnableRowNavigation = schema.HasForms && !schema.IsViewModel,
                ShowHeader = options.ShowHeader,
                ContainerCssClass = options.ContainerCssClass,
                MaxHeight = options.MaxHeight,
                ContextFilters = options.ContextFilters,
                IsEditable = options.IsEditable,
                AutoSave = options.AutoSave,
                FieldName = options.FieldName,
                ParentIdPropertyName = options.ParentIdPropertyName,
                ParentEntityId = options.ParentEntityId,
                EditableColumnNames = options.EditableColumnNames,
                ParentField = options.ParentField
            };

#pragma warning disable CS8601 // BuildApiUrl always returns non-null string
            model.ApiEndpoint = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.BuildApiUrl(moduleName, schema.TableName);
#pragma warning restore CS8601

            model.BodyEndpoint = $"/_content/TipsyBaboon.UI/grid/{moduleName}/{schema.TableName}/body";

            model.Columns = BuildColumns(schema, options.EditableColumnNames);

            model.AllColumns = options.IsEditable ? null : BuildAllColumns(schema);

            model.RowActions = BuildRowActions(schema, options.CanEdit, options.CanDelete);

            model.ToolbarActions = BuildToolbarActions(schema, options.CanCreate, options.CanDelete);

            if (options.UseInlineData && entities.Any())
            {
                model.InitialData = ConvertEntitiesToDictionaries(entities, model.Columns);
            }

            return model;
        }

        private static List<Dictionary<string, object?>> ConvertEntitiesToDictionaries(IEnumerable<object> entities, List<GridColumn> columns)
        {
            var result = new List<Dictionary<string, object?>>();
            var columnFields = columns.Select(c => c.Field).ToHashSet();

            foreach (var entity in entities)
            {
                var dict = new Dictionary<string, object?>();
                var entityType = entity.GetType();

                foreach (var prop in entityType.GetProperties())
                {
                    var isKey = columns.Any(c => c.Field == prop.Name && c.IsKey);
                    if (columnFields.Contains(prop.Name) || isKey)
                    {
                        var value = prop.GetValue(entity);
                        dict[prop.Name] = value;
                    }
                }

                result.Add(dict);
            }

            return result;
        }

        private static List<GridColumn> BuildColumns(SchemaDefinition schema, List<string>? editableColumnNames)
        {
            var columns = new List<GridColumn>();
            var editableSet = editableColumnNames != null
                ? new HashSet<string>(editableColumnNames, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();

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
                    Hidden = !keyCol.ShowInList,
                    FilterType = GetFilterType(keyCol.ClrType),
                    ShowByDefault = keyCol.ShowInList,
                    CanToggle = false
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
                var isEditable = editableSet.Contains(col.PropertyName);
                var editorType = isEditable ? GetEditorType(col.ClrType) : null;
                var enumOptions = isEditable && col.ClrType.IsEnum ? GetEnumOptions(col.ClrType) : null;

                columns.Add(new GridColumn
                {
                    PropertyName = col.PropertyName,
                    DisplayName = col.DisplayName ?? col.PropertyName,
                    PropertyType = col.ClrType,
                    Format = null,
                    Sortable = true,
                    CssClass = string.Empty,
                    Alignment = GetDefaultAlignment(col.ClrType),
                    FormatterType = GetFormatterType(col.ClrType),
                    IsEditable = isEditable,
                    EditorType = editorType,
                    EnumOptions = enumOptions,
                    FilterType = GetFilterType(col.ClrType),
                    ShowByDefault = true,
                    CanToggle = !col.IsPrimaryKey
                });
            }

            return columns;
        }

        private static List<GridColumn> BuildAllColumns(SchemaDefinition schema)
        {
            var columns = new List<GridColumn>();
            var keyPropertyNames = schema.KeyColumns.Select(k => k.PropertyName).ToHashSet();

            foreach (var keyCol in schema.KeyColumns)
            {
                columns.Add(new GridColumn
                {
                    PropertyName = keyCol.PropertyName,
                    DisplayName = keyCol.DisplayName ?? keyCol.PropertyName,
                    PropertyType = keyCol.ClrType,
                    IsKey = true,
                    KeyOrdinal = keyCol.KeyOrdinal,
                    Hidden = !keyCol.ShowInList,
                    ShowByDefault = keyCol.ShowInList,
                    CanToggle = false,
                    FilterType = GetFilterType(keyCol.ClrType)
                });
            }

            var allDisplayableColumns = schema.Columns
                .Where(c => !keyPropertyNames.Contains(c.PropertyName) && !c.IsNavigationProperty)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.PropertyName)
                .ToList();

            foreach (var col in allDisplayableColumns)
            {
                columns.Add(new GridColumn
                {
                    PropertyName = col.PropertyName,
                    DisplayName = col.DisplayName ?? col.PropertyName,
                    PropertyType = col.ClrType,
                    Sortable = true,
                    Alignment = GetDefaultAlignment(col.ClrType),
                    FormatterType = GetFormatterType(col.ClrType),
                    FilterType = GetFilterType(col.ClrType),
                    ShowByDefault = col.ShowInList,
                    CanToggle = true,
                    Hidden = !col.ShowInList
                });
            }

            return columns;
        }

        private static string? GetFilterType(Type propertyType)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(bool))
                return "boolean";

            if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
                underlyingType == typeof(decimal) || underlyingType == typeof(double) ||
                underlyingType == typeof(float) || underlyingType == typeof(short) ||
                underlyingType == typeof(byte))
                return "number";

            if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset) || underlyingType == typeof(DateOnly))
                return "date";

            if (underlyingType.IsEnum)
                return "enum";

            if (underlyingType.IsClass && underlyingType != typeof(string))
                return null;

            return "text";
        }

        private static string? GetEditorType(Type propertyType)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(bool))
                return "checkbox";

            if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
                underlyingType == typeof(decimal) || underlyingType == typeof(double) ||
                underlyingType == typeof(float) || underlyingType == typeof(short) ||
                underlyingType == typeof(byte))
                return "number";

            if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
                return "datetime";

            if (underlyingType == typeof(DateOnly))
                return "date";

            if (underlyingType.IsEnum)
                return "select";

            if (underlyingType == typeof(string))
                return "text";

            if (underlyingType.IsClass || underlyingType.IsArray ||
                (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() != typeof(Nullable<>)))
                return "json";

            return null;
        }

        private static Dictionary<string, string>? GetEnumOptions(Type enumType)
        {
            var underlyingType = Nullable.GetUnderlyingType(enumType) ?? enumType;
            if (!underlyingType.IsEnum)
                return null;

            var options = new Dictionary<string, string>();
            foreach (var value in Enum.GetValues(underlyingType))
            {
                var name = Enum.GetName(underlyingType, value);
                if (name != null)
                {
                    options[Convert.ToInt32(value).ToString()] = name;
                }
            }
            return options;
        }

        private static List<GridRowAction> BuildRowActions(SchemaDefinition schema, bool canEdit, bool canDelete)
        {
            var actions = new List<GridRowAction>();

            if (canDelete)
            {
                actions.Add(new GridRowAction
                {
                    ActionId = "delete",
                    DisplayName = "Delete",
                    Icon = "bi bi-trash",
                    ButtonClass = "btn-danger",
                    Handler = "deleteEntity",
                    RequireConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to delete this item?",
                    Order = 100,
                    IsBuiltIn = true
                });
            }

            var rowActions = schema.Actions
                .Where(a => a.Visible && a.RequiresSelection)
                .OrderBy(a => a.Order);

            foreach (var action in rowActions)
            {
                actions.Add(new GridRowAction
                {
                    ActionId = action.ActionId,
                    DisplayName = action.DisplayName,
                    Icon = action.Icon,
                    ButtonClass = action.ButtonClass,
                    Handler = string.IsNullOrEmpty(action.Handler) ? "executeAction" : action.Handler,
                    UrlPattern = action.UrlPattern,
                    RequireConfirmation = action.RequireConfirmation,
                    ConfirmationMessage = action.ConfirmationMessage,
                    PermissionRequired = action.PermissionRequired,
                    Order = action.Order,
                    IsBuiltIn = false
                });
            }

            return actions.OrderBy(a => a.Order).ToList();
        }

        private static List<GridToolbarAction> BuildToolbarActions(SchemaDefinition schema, bool canCreate, bool canDelete)
        {
            var actions = new List<GridToolbarAction>();

            if (canCreate)
            {
                actions.Add(new GridToolbarAction
                {
                    ActionId = "new",
                    DisplayName = "New",
                    Icon = "bi bi-plus",
                    ButtonClass = "btn-success",
                    Handler = "newEntity",
                    RequiresSelection = false,
                    Order = 10,
                    IsBuiltIn = true
                });
            }

            var toolbarActions = schema.Actions
                .Where(a => a.Visible)
                .OrderBy(a => a.Order);

            foreach (var action in toolbarActions)
            {
                actions.Add(new GridToolbarAction
                {
                    ActionId = action.ActionId,
                    DisplayName = action.DisplayName,
                    Icon = action.Icon,
                    ButtonClass = action.ButtonClass,
                    Handler = string.IsNullOrEmpty(action.Handler) ? "executeAction" : action.Handler,
                    Url = action.UrlPattern,
                    RequiresSelection = action.RequiresSelection,
                    RequireConfirmation = action.RequireConfirmation,
                    ConfirmationMessage = action.ConfirmationMessage,
                    PermissionRequired = action.PermissionRequired,
                    Order = action.Order,
                    Position = action.ToolbarPosition,
                    IsBuiltIn = false
                });
            }

            if (canDelete)
            {
                actions.Add(new GridToolbarAction
                {
                    ActionId = "delete",
                    DisplayName = "Delete",
                    Icon = "bi bi-trash",
                    ButtonClass = "btn-danger",
                    Handler = "deleteEntity",
                    RequiresSelection = true,
                    RequireConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to delete the selected item(s)?",
                    Order = 1000,
                    IsBuiltIn = true
                });
            }

            return actions.OrderBy(a => a.Order).ToList();
        }

        private static string GetDefaultAlignment(Type propertyType)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(int) ||
                underlyingType == typeof(long) ||
                underlyingType == typeof(decimal) ||
                underlyingType == typeof(double) ||
                underlyingType == typeof(float))
            {
                return "right";
            }

            if (underlyingType == typeof(bool))
            {
                return "center";
            }

            return "left";
        }

        private static string? GetFormatterType(Type propertyType)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(bool))
            {
                return "Boolean";
            }

            if (underlyingType == typeof(DateTime))
            {
                return "DateTime";
            }

            if (underlyingType == typeof(DateOnly))
            {
                return "Date";
            }

            return null;
        }
    }

    public class GridBuildOptions
    {
        public string? GridId { get; set; }
        public bool EnableModalView { get; set; } = true;
        public bool CanCreate { get; set; } = true;
        public bool CanEdit { get; set; } = true;
        public bool CanDelete { get; set; } = true;
        public int PageSize { get; set; } = 25;
        public int CurrentPage { get; set; } = 1;
        public int TotalCount { get; set; }
        public bool ShowHeader { get; set; } = true;
        public string? ContainerCssClass { get; set; }
        public string? MaxHeight { get; set; }
        public Dictionary<string, object?>? ContextFilters { get; set; }
        public bool IsEditable { get; set; }
        public bool AutoSave { get; set; }
        public List<string>? EditableColumnNames { get; set; }
        public string? FieldName { get; set; }
        public string? ParentIdPropertyName { get; set; }
        public string? ParentEntityId { get; set; }
        public bool UseInlineData { get; set; }
        public string? ParentField { get; set; }
        public string? ParentTypeName { get; set; }
    }
}
