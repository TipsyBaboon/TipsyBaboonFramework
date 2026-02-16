using System;

namespace TipsyBaboon.Core.FrameworkBase
{
    /// <summary>
    /// Abstract base class for user preference classes.
    /// User preferences are stored per-user in the database and can be managed through the UI.
    /// Derived classes should be marked with [UserPreferenceClass] attribute.
    /// </summary>
    public abstract class UserPreferenceBase
    {
        /// <summary>
        /// Module name this user preference belongs to.
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        /// Initializes a new user preference instance for the specified module.
        /// </summary>
        /// <param name="moduleName">Module name.</param>
        /// <exception cref="ArgumentException">Thrown if module name is null or empty.</exception>
        protected UserPreferenceBase(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            
            ModuleName = moduleName;
        }
    }
}
