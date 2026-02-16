using TipsyBaboon.Core;

namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// Navigation menu item for building hierarchical menus.
    /// Code-only model used by MenuBuilder for in-memory menu construction.
    /// </summary>
    public class MenuItem
    {
        public string Label { get; set; } = string.Empty;

        public string? Icon { get; set; }

        public MenuItemType Type { get; set; } = MenuItemType.CustomLink;

        public string? Url { get; set; }

        public string? ModelTypeName { get; set; }

        public string? PermissionRequired { get; set; }

        public int Order { get; set; } = 0;

        public List<MenuItem> Children { get; set; } = new();

        public bool IsVisible { get; set; } = true;

        public string? Target { get; set; }

        public string? CssClass { get; set; }

        public string? Tooltip { get; set; }
    }

    /// <summary>
    /// Container for the root navigation menu structure.
    /// Holds top-level menu items and tracking metadata.
    /// </summary>
    public class Menu
    {
        public List<MenuItem> RootItems { get; set; } = new();

        public DateTime? LastGenerated { get; set; }
    }
}
