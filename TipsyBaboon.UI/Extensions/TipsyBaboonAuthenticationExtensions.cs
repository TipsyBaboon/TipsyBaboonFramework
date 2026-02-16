using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Extensions
{
    /// <summary>
    /// Extension methods for registering TipsyBaboon Identity (authentication) pages.
    /// </summary>
    public static class TipsyBaboonAuthenticationExtensions
    {
        /// <summary>
        /// Registers the embedded TipsyBaboon Identity Area pages (Login, Register, Logout, Password Reset, etc.)
        /// and configures Razor Pages authorization policies. Call on the <see cref="IMvcBuilder"/> returned
        /// by <c>AddRazorPages()</c>.
        /// </summary>
        /// <param name="mvcBuilder">The MVC builder.</param>
        /// <param name="configure">Optional callback to configure <see cref="TipsyBaboonIdentityPageOptions"/>.</param>
        /// <returns>The MVC builder for chaining.</returns>
        public static IMvcBuilder AddTipsyBaboonIdentityPages(
            this IMvcBuilder mvcBuilder,
            Action<TipsyBaboonIdentityPageOptions>? configure = null)
        {
            var options = new TipsyBaboonIdentityPageOptions();
            configure?.Invoke(options);

            mvcBuilder.Services.AddSingleton(options);

            var assembly = typeof(TipsyBaboonAuthenticationExtensions).Assembly;
            mvcBuilder.PartManager.ApplicationParts.Add(new CompiledRazorAssemblyPart(assembly));

            mvcBuilder.Services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(razorOptions =>
            {
                razorOptions.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Logout");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Register");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPasswordConfirmation");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPassword");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPasswordConfirmation");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ExternalLogin");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/AccessDenied");
            });

            return mvcBuilder;
        }
    }
}
