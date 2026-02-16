using System;

namespace TipsyBaboon.Core.FrameworkBase
{
    /// <summary>
    /// Abstract base class for module configuration classes.
    /// Configuration properties are stored in the database and can be managed through the UI.
    /// Derived classes should be marked with [ConfigurationClass] attribute.
    /// </summary>
    public abstract class ConfigurationBase
    {
        /// <summary>
        /// Module name this configuration belongs to.
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        /// Initializes a new configuration instance for the specified module.
        /// </summary>
        /// <param name="moduleName">Module name.</param>
        /// <exception cref="ArgumentException">Thrown if module name is null or empty.</exception>
        protected ConfigurationBase(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            
            ModuleName = moduleName;
        }
        
        /// <summary>
        /// Sets a property value from string. Used by ModelRegistry.AddInvariantConfig to populate
        /// static configuration instances during module registration.
        /// </summary>
        protected static void SetConfigValue<T>(ref T? staticInstance, string propertyName, string value) where T : ConfigurationBase, new()
        {
            staticInstance ??= new T();
            var property = typeof(T).GetProperty(propertyName);
            if (property == null) return;
            
            if (property.PropertyType == typeof(bool))
            {
                if (bool.TryParse(value, out var boolValue))
                    property.SetValue(staticInstance, boolValue);
            }
            else if (property.PropertyType == typeof(string))
            {
                property.SetValue(staticInstance, value);
            }
            else if (property.PropertyType == typeof(int))
            {
                if (int.TryParse(value, out var intValue))
                    property.SetValue(staticInstance, intValue);
            }
            else if (property.PropertyType == typeof(double))
            {
                if (double.TryParse(value, out var doubleValue))
                    property.SetValue(staticInstance, doubleValue);
            }
            else if (property.PropertyType == typeof(decimal))
            {
                if (decimal.TryParse(value, out var decimalValue))
                    property.SetValue(staticInstance, decimalValue);
            }
            // Add more type handlers as needed
        }
    }
}
