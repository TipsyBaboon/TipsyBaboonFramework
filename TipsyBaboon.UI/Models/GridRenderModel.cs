using System.Text.Json.Serialization;

namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// Complete grid configuration model - designed to be JSON-serializable for JavaScript grid initialization.
    /// </summary>
    public class GridRenderModel
    {
        public string GridId { get; set; } = Guid.NewGuid().ToString("N");

        public string FormId { get; set; } = Guid.NewGuid().ToString();

        public string ApiEndpoint { get; set; } = string.Empty;

        [JsonIgnore]
        public Type EntityType { get; set; } = null!;

        public string EntityDisplayName { get; set; } = string.Empty;

        public string EntitySingularName { get; set; } = string.Empty;

        public string EntityTypeName { get; set; } = string.Empty;

        public string ModuleName { get; set; } = string.Empty;

        [JsonIgnore]
        public IEnumerable<object> Entities { get; set; } = Array.Empty<object>();

        public List<GridColumn> Columns { get; set; } = new();

        public List<GridRowAction> RowActions { get; set; } = new();

        public List<GridToolbarAction> ToolbarActions { get; set; } = new();

        public bool EnableModalView { get; set; } = true;

        public bool? CreateUseModal { get; set; }

        public bool? EditUseModal { get; set; }

        public string? DetailUrlOverride { get; set; }

        public bool CanCreate { get; set; }

        public bool CanEdit { get; set; }

        public bool CanDelete { get; set; }

        public int PageSize { get; set; } = 25;

        public int CurrentPage { get; set; } = 1;

        public int TotalCount { get; set; }

        public bool EnablePaging { get; set; } = true;

        public bool EnableSelection { get; set; } = true;

        public bool EnableRowNavigation { get; set; } = true;

        public bool ShowHeader { get; set; } = true;

        public string? ContainerCssClass { get; set; }

        public string PageRoutePrefix { get; set; } = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.PageRoutePrefix;

        public string ApiRoutePrefix { get; set; } = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.ApiRoutePrefix;

        public string? MaxHeight { get; set; }

        public Dictionary<string, object?>? ContextFilters { get; set; }

        public bool IsEditable { get; set; }

        public bool AutoSave { get; set; }

        public string? FieldName { get; set; }

        public string? ParentIdPropertyName { get; set; }

        public string? ParentEntityId { get; set; }

        public List<Dictionary<string, object?>>? InitialData { get; set; }

        public string? BodyEndpoint { get; set; }

        public List<string>? EditableColumnNames { get; set; }

        public string? ParentField { get; set; }

        public bool AutoSaveGridLayouts { get; set; } = true;

        public List<GridColumn>? AllColumns { get; set; }
    }

    /// <summary>
    /// Grid column definition - JSON-serializable for JavaScript.
    /// PropertyName/DisplayName are preserved for C# compatibility, Field/Header for JS.
    /// </summary>
    public class GridColumn
    {
        public string Field { get; set; } = string.Empty;

        public string PropertyName
        {
            get => Field;
            set => Field = value;
        }

        public string Header { get; set; } = string.Empty;

        public string DisplayName
        {
            get => Header;
            set => Header = value;
        }

        [JsonIgnore]
        public Type PropertyType { get; set; } = typeof(string);

        public string? Format { get; set; }

        public bool Sortable { get; set; } = true;

        public string? Width { get; set; }

        public string? CssClass { get; set; }

        public string Alignment { get; set; } = "left";

        public string? FormatterType { get; set; }

        public bool IsEditable { get; set; }

        public string? EditorType { get; set; }

        public Dictionary<string, string>? EnumOptions { get; set; }

        public bool IsKey { get; set; }

        public int? KeyOrdinal { get; set; }

        public bool Hidden { get; set; }

        public bool ShowByDefault { get; set; } = true;

        public bool CanToggle { get; set; } = true;

        public string? FilterType { get; set; }
    }

    /// <summary>
    /// Grid row/context menu action - JSON-serializable for JavaScript.
    /// ActionId/DisplayName are preserved for C# compatibility, Id/Label for JS.
    /// </summary>
    public class GridRowAction
    {
        public string Id { get; set; } = string.Empty;

        public string ActionId
        {
            get => Id;
            set => Id = value;
        }

        public string Label { get; set; } = string.Empty;

        public string DisplayName
        {
            get => Label;
            set => Label = value;
        }

        public string? Icon { get; set; }

        public string? ButtonClass { get; set; } = "btn-secondary";

        public string? Handler { get; set; }

        public string? UrlPattern { get; set; }

        public bool RequireConfirmation { get; set; }

        public string? ConfirmationMessage { get; set; }

        public string? PermissionRequired { get; set; }

        public int Order { get; set; } = 100;

        public bool IsBuiltIn { get; set; }
    }

    /// <summary>
    /// Grid toolbar action - JSON-serializable for JavaScript.
    /// ActionId/DisplayName are preserved for C# compatibility, Id/Label for JS.
    /// </summary>
    public class GridToolbarAction
    {
        public string Id { get; set; } = string.Empty;

        public string ActionId
        {
            get => Id;
            set => Id = value;
        }

        public string Label { get; set; } = string.Empty;

        public string DisplayName
        {
            get => Label;
            set => Label = value;
        }

        public string? Icon { get; set; }

        public string? ButtonClass { get; set; } = "btn-primary";

        public string? CssClass
        {
            get => ButtonClass;
            set => ButtonClass = value ?? "btn-primary";
        }

        public string? Handler { get; set; }

        public string? Url { get; set; }

        public bool RequiresSelection { get; set; }

        public bool RequireConfirmation { get; set; }

        public string? ConfirmationMessage { get; set; }

        public string? PermissionRequired { get; set; }

        public int Order { get; set; } = 100;

        public string? Position { get; set; } = "left";

        public bool IsBuiltIn { get; set; }
    }

    /// <summary>
    /// DTO for saving/restoring grid column and sort state to the layout API.
    /// </summary>
    public class GridStateDto
    {
        public List<GridColumnState> Columns { get; set; } = new();
        public List<GridSortState> Sort { get; set; } = new();
    }

    public class GridColumnState
    {
        public string Field { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Visible { get; set; } = true;
    }

    public class GridSortState
    {
        public string Field { get; set; } = string.Empty;
        public string Direction { get; set; } = "asc";
        public int Ordinal { get; set; }
    }
}
