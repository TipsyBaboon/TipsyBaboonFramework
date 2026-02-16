using System.Text.Json;
using System.Text.Json.Serialization;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Provides the shared <see cref="JsonSerializerOptions"/> used by all TipsyBaboon API controllers
    /// and services for consistent JSON serialization (PascalCase naming, enum-as-string, null-ignore, cycle handling).
    /// </summary>
    public static class TipsyBaboonJsonOptions
    {
        private static JsonSerializerOptions? _options;

        /// <summary>Gets the singleton <see cref="JsonSerializerOptions"/> instance, lazily initialized.</summary>
        public static JsonSerializerOptions Default
        {
            get
            {
                if (_options == null)
                {
                    _options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = null,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        PropertyNameCaseInsensitive = true,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles,
                        MaxDepth = 64,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString
                    };
                    _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
                }
                return _options;
            }
        }
    }
}
