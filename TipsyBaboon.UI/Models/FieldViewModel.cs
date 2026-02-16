using System.Text.Json;
using TipsyBaboon.Core.Data;

namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// Strongly-typed view model passed to field partial views during rendering.
    /// Carries the column metadata, parent schema, current value, and computed input names/IDs
    /// for both form and inline-grid editing contexts.
    /// </summary>
    public class FieldViewModel<T>
    {
        public required ColumnDefinition Column { get; init; }

        public required SchemaDefinition Schema { get; init; }

        public T Value { get; init; } = default!;

        public object? Instance { get; init; }

        public string FormId { get; init; } = string.Empty;

        public bool WasNull { get; init; }

        public bool IsGridContext { get; init; }

        public string? RowId { get; init; }

        public string? FieldName { get; init; }

        /// <summary>
        /// Gets the computed input name for this field.
        /// For grid context: {FieldName}[{RowId}].{PropertyName} or just PropertyName
        /// For form context: {FormId}-{PropertyName}
        /// </summary>
        public string InputName => IsGridContext && !string.IsNullOrEmpty(FieldName) && !string.IsNullOrEmpty(RowId)
            ? $"{FieldName}[{RowId}].{Column.PropertyName}"
            : IsGridContext && !string.IsNullOrEmpty(RowId)
                ? Column.PropertyName
                : Column.PropertyName;

        /// <summary>
        /// Gets the computed element ID for this field.
        /// </summary>
        public string ElementId => IsGridContext && !string.IsNullOrEmpty(RowId)
            ? $"{FormId}-{RowId}-{Column.PropertyName}"
            : $"{FormId}-{Column.PropertyName}";

        /// <summary>
        /// Gets the module name from the schema.
        /// </summary>
        public string ModuleName => Schema.ModuleName;

        /// <summary>
        /// Gets the model type name from the schema.
        /// </summary>
        public string ModelName => Schema.TypeName;

        /// <summary>
        /// Gets the record keys as a JSON object.
        /// Extracts key column values from the Instance.
        /// </summary>
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

        /// <summary>
        /// Gets the control configuration dictionary from the column definition.
        /// </summary>
        public Dictionary<string, string>? ControlConfig => Column.ControlConfig;

        public string WrapperRecordContextAttributes =>
            $"data-module=\"{ModuleName}\" data-model=\"{ModelName}\" data-record-keys='{System.Net.WebUtility.HtmlEncode(RecordKeysJson)}'";

        /// <summary>
        /// Gets the data attribute for the field element itself (just the field name).
        /// </summary>
        public string FieldContextAttribute =>
            $"data-field=\"{Column.PropertyName}\"";
    }

}
