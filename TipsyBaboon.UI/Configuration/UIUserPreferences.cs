using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.FrameworkBase;

namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Per-user preference settings for the UI layer, stored as a <see cref="UserPreferenceBase"/> record.
    /// Discovered via <see cref="UserPreferenceClassAttribute"/> and editable from the user's Manage/Preferences page.
    /// </summary>
    [UserPreferenceClass]
    [ModuleName("TipsyBaboon.UI")]
    [UIEntity("UserPreferences", ShowInMenu = false, Description = "User Preference Settings")]
    [UIGroup("Grid", Title = "Grid Settings", Order = 1)]
    public class UIUserPreferences : UserPreferenceBase
    {
        public UIUserPreferences() : base("TipsyBaboon.UI")
        {
        }

        [UIDisplay(Order = 1, Name = "Auto-Save Grid Layouts", GroupName = "Grid", Description = "Automatically save grid column layouts when changed")]
        public bool AutoSaveGridLayouts { get; set; } = true;

        [UIDisplay(Order = 2, Name = "Default Page Size", GroupName = "Grid", Description = "Default number of rows to display in entity grids")]
        public int DefaultPageSize { get; set; } = 25;
    }
}
