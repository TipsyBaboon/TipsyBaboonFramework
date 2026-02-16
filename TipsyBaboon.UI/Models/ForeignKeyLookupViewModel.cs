using System.Text.Json;
using TipsyBaboon.Core.Data;

namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// View model for rendering a foreign-key lookup field, providing schema metadata for both
    /// the owning entity and the related entity, plus the current FK value and display text.
    /// </summary>
    public class ForeignKeyLookupViewModel
    {
        public required ColumnDefinition Column { get; init; }

        public required SchemaDefinition Schema { get; init; }

        public required SchemaDefinition RelatedSchema { get; init; }

        public object? Value { get; init; }

        public string? DisplayValue { get; init; }

        public object? Instance { get; init; }

        public string FormId { get; init; } = string.Empty;

        public bool WasNull { get; init; }

        public bool IsGridContext { get; init; }

        public string? RowId { get; init; }

        public string? FieldName { get; init; }

        public bool EnableModalView { get; init; } = true;

        public string InputName => IsGridContext && !string.IsNullOrEmpty(FieldName) && !string.IsNullOrEmpty(RowId)
            ? $"{FieldName}[{RowId}].{Column.PropertyName}"
            : Column.PropertyName;

        public string ElementId => IsGridContext && !string.IsNullOrEmpty(RowId)
            ? $"{FormId}-{RowId}-{Column.PropertyName}"
            : $"{FormId}-{Column.PropertyName}";

        public string ModuleName => Schema.ModuleName;

        public string ModelName => Schema.TypeName;

        public string RecordKeysJson
        {
            get
            {
                if (Instance == null)
                    return "{}";

                var keyColumns = Schema.KeyColumns;
                if (keyColumns.Count == 0)
                    return "{}";

                var keys = keyColumns.ToDictionary(
                    k => k.PropertyName,
                    k => Instance.GetType().GetProperty(k.PropertyName)?.GetValue(Instance)?.ToString() ?? ""
                );

                return JsonSerializer.Serialize(keys);
            }
        }

        public Dictionary<string, string>? ControlConfig => Column.ControlConfig;

        public string WrapperRecordContextAttributes =>
            $"data-module=\"{ModuleName}\" data-model=\"{ModelName}\" data-record-keys='{System.Net.WebUtility.HtmlEncode(RecordKeysJson)}'";

        public string FieldContextAttribute =>
            $"data-field=\"{Column.PropertyName}\"";
    }
}
