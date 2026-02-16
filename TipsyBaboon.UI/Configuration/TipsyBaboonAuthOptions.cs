namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Options for the embedded Identity area pages, controlling layout, feature toggles,
    /// and whether the identity pages are registered at all.
    /// Configured via <see cref="Extensions.TipsyBaboonAuthenticationExtensions.AddTipsyBaboonIdentityPages"/>.
    /// </summary>
    public class TipsyBaboonIdentityPageOptions
    {
        public string LayoutPage { get; set; } = "/Pages/Shared/_Layout.cshtml";
        public bool EnableManagePages { get; set; } = true;
        public bool Enabled { get; set; } = true;
    }
}
