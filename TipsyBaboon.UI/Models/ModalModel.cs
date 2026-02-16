namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// Configuration model for a Bootstrap modal dialog rendered by the shared modal partial view.
    /// Controls title, size, buttons, and content source (URL or inline HTML).
    /// </summary>
    public class ModalModel
    {
        public string? ModalId { get; set; }

        public string Title { get; set; } = "Modal";

        public string Size { get; set; } = "xl";

        public bool Scrollable { get; set; } = true;

        public string? EntityName { get; set; }

        public bool ShowNewWindowLink { get; set; } = true;

        public string? ContentUrl { get; set; }

        public string? Content { get; set; }

        public bool ShowCloseButton { get; set; } = true;

        public string CloseButtonText { get; set; } = "Close";

        public bool ShowSaveButton { get; set; } = false;

        public string SaveButtonText { get; set; } = "Save Changes";
    }
}
