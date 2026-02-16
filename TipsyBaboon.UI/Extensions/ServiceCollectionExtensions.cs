using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Services;

namespace TipsyBaboon.UI.Extensions
{
    /// <summary>
    /// Extension methods for registering TipsyBaboon UI layer services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all TipsyBaboon UI services including permission checks, layout management,
        /// menu building, user governance, and impersonation session cleanup.
        /// Call after <c>services.AddTipsyBaboon()</c> has been configured with a provider.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTipsyBaboonUI(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<ILayoutService, LayoutService>();

            services.AddScoped<MenuBuilder>();

            services.AddScoped<UserGovernanceService>();

            services.AddHostedService<ImpersonationCleanupService>();

            return services;
        }
    }
}
