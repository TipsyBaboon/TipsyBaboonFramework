using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account
{
    public class LogoutModel : IdentityPageModel
    {
        private readonly BaseModelStore _store;

        public LogoutModel(TipsyBaboonIdentityPageOptions options, BaseModelStore store) : base(options)
        {
            _store = store;
        }

        public async Task<IActionResult> OnPost(string? returnUrl = null)
        {
            await CleanupImpersonationSessionsAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            returnUrl ??= Request.Headers["Referer"].ToString();
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }

        public async Task<IActionResult> OnGet(string? returnUrl = null)
        {
            await CleanupImpersonationSessionsAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            returnUrl ??= Request.Headers["Referer"].ToString();
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }

        private async Task CleanupImpersonationSessionsAsync()
        {
            var currentUserIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdClaim) || !Guid.TryParse(currentUserIdClaim, out var currentUserId))
                return;

            var sessionsAsImpersonated = await _store.QueryTypedAsync<ImpersonationSession>(
                s => s.ImpersonatedUserId == currentUserId);
            foreach (var session in sessionsAsImpersonated)
            {
                await _store.DeleteTypedAsync<ImpersonationSession>(session.Id);
            }

            var sessionsAsImpersonator = await _store.QueryTypedAsync<ImpersonationSession>(
                s => s.ImpersonatingUserId == currentUserId);
            foreach (var session in sessionsAsImpersonator)
            {
                await _store.DeleteTypedAsync<ImpersonationSession>(session.Id);
            }
        }
    }
}
