using System;
using Microsoft.Extensions.DependencyInjection;

namespace TipsyBaboon.Core.Startup
{
    /// <summary>
    /// Service collection extensions for registering TipsyBaboon framework services.
    /// </summary>
    public static class TipsyBaboonServiceCollectionExtensions
    {
        /// <summary>
        /// Registers TipsyBaboon framework services and modules.
        /// Use the builder to register modules, select a provider, and optionally add seed/invariant data.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configuration action with TipsyBaboonBuilder.</param>
        /// <returns>Service collection for chaining.</returns>
        /// <example>
        /// services.AddTipsyBaboon(builder => {
        ///     builder.UseSqlServer();
        ///     builder.AddModule("Governance", connectionString, "Core governance module");
        ///     builder.AddModule("MyApp", connectionString, "Application module");
        /// });
        /// </example>
        public static IServiceCollection AddTipsyBaboon(
            this IServiceCollection services,
            Action<TipsyBaboonBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var builder = new TipsyBaboonBuilder(services);
            configure(builder);
            builder.Build();

            return services;
        }
    }
}
