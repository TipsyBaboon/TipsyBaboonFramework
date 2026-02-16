using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages
{
    /// <summary>
    /// Base page model for all embedded Identity area pages (Login, Register, etc.).
    /// Provides the configurable layout page path from <see cref="TipsyBaboonIdentityPageOptions"/>.
    /// </summary>
    public abstract class IdentityPageModel : PageModel
    {
        private readonly TipsyBaboonIdentityPageOptions? _options;

        protected IdentityPageModel() { }

        protected IdentityPageModel(TipsyBaboonIdentityPageOptions options)
        {
            _options = options;
        }

        public string LayoutPage => _options?.LayoutPage ?? "/Pages/Shared/_Layout.cshtml";
    }
}
