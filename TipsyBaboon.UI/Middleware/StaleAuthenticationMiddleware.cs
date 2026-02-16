using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using IAppAuthenticationService = TipsyBaboon.Core.Interfaces.IAuthenticationService;

namespace TipsyBaboon.UI.Middleware
{
    /// <summary>
    /// Middleware that validates the authenticated user still exists and is active on every request.
    /// Signs the user out and clears the principal if the account has been deactivated or deleted.
    /// </summary>
    public class StaleAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;

        public StaleAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAppAuthenticationService authService)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    var user = await authService.FindByIdAsync(userId);
                    if (user == null || !user.IsActive)
                    {
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.User = new ClaimsPrincipal(new ClaimsIdentity());
                    }
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for registering <see cref="StaleAuthenticationMiddleware"/> in the ASP.NET Core pipeline.
    /// </summary>
    public static class StaleAuthenticationMiddlewareExtensions
    {
        /// <summary>
        /// Adds the stale authentication check middleware to the pipeline.
        /// Should be placed after <c>UseAuthentication</c> so the user principal is available.
        /// </summary>
        public static IApplicationBuilder UseStaleAuthenticationCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<StaleAuthenticationMiddleware>();
        }
    }
}
