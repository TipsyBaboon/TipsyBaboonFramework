using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.UI.Middleware
{
    /// <summary>
    /// Middleware that extends impersonation session timeout on activity to match session timeout.
    /// </summary>
    public class ImpersonationKeepaliveMiddleware
    {
        private readonly RequestDelegate _next;

        public ImpersonationKeepaliveMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, BaseModelStore store)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var impersonationKeyClaim = context.User.FindFirst("ImpersonationKey")?.Value;
                if (!string.IsNullOrEmpty(impersonationKeyClaim) && Guid.TryParse(impersonationKeyClaim, out var impersonationKey))
                {
                    var lastKeepaliveKey = $"ImpersonationKeepalive_{impersonationKey}";
                    var lastKeepalive = context.Session.GetString(lastKeepaliveKey);
                    var shouldUpdate = false;

                    if (string.IsNullOrEmpty(lastKeepalive))
                    {
                        shouldUpdate = true;
                    }
                    else if (DateTime.TryParse(lastKeepalive, out var lastUpdate))
                    {
                        if ((DateTime.UtcNow - lastUpdate).TotalMinutes > 5)
                        {
                            shouldUpdate = true;
                        }
                    }

                    if (shouldUpdate)
                    {
                        try
                        {
                            var sessions = await store.QueryTypedAsync<ImpersonationSession>(
                                s => s.ImpersonationKey == impersonationKey);
                            var session = sessions.FirstOrDefault();

                            if (session != null && session.ExpiresOn > DateTime.UtcNow)
                            {
                                session.ExpiresOn = DateTime.UtcNow.AddHours(2);
                                await store.UpdateTypedAsync<ImpersonationSession>(session);
                                context.Session.SetString(lastKeepaliveKey, DateTime.UtcNow.ToString("O"));
                            }
                        }
                        catch
                        {
                            // Silently fail keepalive - don't break the request
                        }
                    }
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for registering <see cref="ImpersonationKeepaliveMiddleware"/> in the ASP.NET Core pipeline.
    /// </summary>
    public static class ImpersonationKeepaliveMiddlewareExtensions
    {
        /// <summary>
        /// Adds the impersonation keepalive middleware to the pipeline.
        /// Should be placed after authentication middleware.
        /// </summary>
        public static IApplicationBuilder UseImpersonationKeepalive(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ImpersonationKeepaliveMiddleware>();
        }
    }
}
