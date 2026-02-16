using TipsyBaboon.Core.Data;

namespace TipsyBaboon.UI.Rendering
{
    /// <summary>
    /// Produces HTML5 data-validation attributes from <see cref="ColumnDefinition"/> metadata,
    /// enabling client-side validation (required, length, range, regex, compare) without hardcoded attribute strings.
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Builds a dictionary of HTML5 data-val-* attributes for client-side validation based on the column's constraints.
        /// </summary>
        /// <param name="column">The column definition containing validation metadata.</param>
        /// <returns>A dictionary of attribute name/value pairs ready for rendering into HTML.</returns>
        public static Dictionary<string, string> GetValidationAttributes(ColumnDefinition column)
        {
            var attributes = new Dictionary<string, string>();

            bool hasValidation = column.IsRequired ||
                                 column.MaxLength > 0 ||
                                 column.MinLength.HasValue ||
                                 column.RangeMin != null ||
                                 column.Pattern != null ||
                                 column.CompareProperty != null;

            if (!hasValidation)
                return attributes;

            attributes["data-val"] = "true";
            var displayName = column.DisplayName ?? column.PropertyName;

            if (column.IsRequired)
            {
                attributes["data-val-required"] = column.RequiredErrorMessage ??
                    $"The {displayName} field is required.";
            }

            if (column.MaxLength > 0 || column.MinLength.HasValue)
            {
                if (column.MinLength.HasValue && column.MaxLength > 0)
                {
                    attributes["data-val-length"] = column.LengthErrorMessage ??
                        $"The field {displayName} must be a string with a minimum length of {column.MinLength} and maximum length of {column.MaxLength}.";
                    attributes["data-val-length-min"] = column.MinLength.Value.ToString();
                    attributes["data-val-length-max"] = column.MaxLength.ToString();
                }
                else if (column.MaxLength > 0)
                {
                    attributes["data-val-maxlength"] = column.LengthErrorMessage ??
                        $"The field {displayName} must be a string with a maximum length of {column.MaxLength}.";
                    attributes["data-val-maxlength-max"] = column.MaxLength.ToString();
                }
                else if (column.MinLength.HasValue)
                {
                    attributes["data-val-minlength"] = column.LengthErrorMessage ??
                        $"The field {displayName} must be a string with a minimum length of {column.MinLength}.";
                    attributes["data-val-minlength-min"] = column.MinLength.Value.ToString();
                }
            }

            if (column.RangeMin != null || column.RangeMax != null)
            {
                attributes["data-val-range"] = column.RangeErrorMessage ??
                    $"The field {displayName} must be between {column.RangeMin} and {column.RangeMax}.";
                if (column.RangeMin != null)
                    attributes["data-val-range-min"] = column.RangeMin.ToString() ?? "";
                if (column.RangeMax != null)
                    attributes["data-val-range-max"] = column.RangeMax.ToString() ?? "";
            }

            if (column.Pattern != null)
            {
                attributes["data-val-regex"] = column.PatternErrorMessage ??
                    $"The field {displayName} must match the required pattern.";
                attributes["data-val-regex-pattern"] = column.Pattern;
            }

            if (column.CompareProperty != null)
            {
                attributes["data-val-equalto"] = column.CompareErrorMessage ??
                    $"'{displayName}' and '{column.CompareProperty}' do not match.";
                attributes["data-val-equalto-other"] = $"*.{column.CompareProperty}";
            }

            return attributes;
        }

        /// <summary>
        /// Returns the validation attributes as a pre-formatted HTML attribute string (space-prefixed).
        /// </summary>
        public static string FormatValidationAttributes(ColumnDefinition column)
        {
            var attrs = GetValidationAttributes(column);
            if (!attrs.Any())
                return string.Empty;

            return " " + string.Join(" ", attrs.Select(kvp => $"{kvp.Key}=\"{System.Net.WebUtility.HtmlEncode(kvp.Value)}\""));
        }

        /// <summary>
        /// Determines whether a column should render as read-only based on its metadata (IsReadOnly, IsUIReadOnly, or IsPrimaryKey).
        /// </summary>
        public static bool IsReadOnly(ColumnDefinition column)
        {
            if (column.IsReadOnly || column.IsUIReadOnly)
                return true;

            if (column.IsPrimaryKey)
                return true;

            return false;
        }
    }
}
