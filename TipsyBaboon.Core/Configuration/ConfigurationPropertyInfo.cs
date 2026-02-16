using System;

namespace TipsyBaboon.Core.Configuration
{
    /// <summary>
    /// Metadata about a configuration property for UI rendering and management.
    /// Used by IConfigurationPageService to expose configuration settings.
    /// </summary>
    public class ConfigurationPropertyInfo
    {
        /// <summary>
        /// Property name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Display name for UI rendering.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>
        /// Description of the configuration property.
        /// </summary>
        public string? Description { get; set; }
        /// <summary>
        /// Group name for organizing properties in UI.
        /// </summary>
        public string? GroupName { get; set; }
        /// <summary>
        /// Display order within group.
        /// </summary>
        public int Order { get; set; }
        /// <summary>
        /// Property type (string, int, bool, etc.).
        /// </summary>
        public Type PropertyType { get; set; } = typeof(string);
        /// <summary>
        /// Current property value.
        /// </summary>
        public object? Value { get; set; }
    }
}