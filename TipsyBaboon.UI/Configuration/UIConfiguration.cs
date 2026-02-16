using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.FrameworkBase;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Persisted UI configuration settings stored as a <see cref="ConfigurationBase"/> record.
    /// Discovered via <see cref="ConfigurationClassAttribute"/> and exposed through the Configuration management page.
    /// Provides authentication and form behavior toggles that apply globally.
    /// </summary>
    [ConfigurationClass]
    [ModuleName("TipsyBaboon.UI")]
    [UIEntity("Configuration", ShowInMenu = false, Description = "UI Configuration Settings")]
    [UIGroup("Authentication", Title = "Authentication", Order = 1)]
    [UIGroup("Forms", Title = "Form Settings", Order = 2)]
    public class UIConfiguration : ConfigurationBase
    {
        private static UIConfiguration? _staticInstance;

        /// <summary>
        /// Gets the statically configured instance populated during module registration.
        /// This is available before database initialization for startup configuration.
        /// </summary>
        public static UIConfiguration Instance => _staticInstance ??= new UIConfiguration();

        /// <summary>
        /// Internal method called by ModelRegistry.AddInvariantConfig to populate the static instance.
        /// </summary>
        internal static void SetValue(string propertyName, string value)
        {
            SetConfigValue(ref _staticInstance, propertyName, value);
        }

        public UIConfiguration() : base("TipsyBaboon.UI")
        {
        }

        [UIDisplay(Order = 1, Name = "Allow Registration", GroupName = "Authentication", Description = "Allow new users to register accounts")]
        public bool AllowRegistration { get; set; } = true;

        [UIDisplay(Order = 10, Name = "Modal View on Double-Click", GroupName = "Forms", Description = "Open entity details in a modal when double-clicking a grid row")]
        public bool EnableModalView { get; set; } = true;

        [UIDisplay(Order = 20, Name = "Auto-Save Changes", GroupName = "Forms", Description = "Automatically save changes when editing fields. When disabled, shows a Save button.")]
        public bool AutoSave { get; set; } = true;
    }
}
