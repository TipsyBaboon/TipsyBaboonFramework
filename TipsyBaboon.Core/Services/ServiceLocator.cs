using System;

namespace TipsyBaboon.Core.Services
{
    /// <summary>
    /// Static service locator for accessing the root service provider.
    /// Must be initialized by consumer code via <see cref="SetProvider"/> during application startup.
    /// Use dependency injection in preference to this where possible.
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// Sets the service provider. Call this in Program.cs after building the app:
        /// <c>ServiceLocator.SetProvider(app.Services);</c>
        /// </summary>
        /// <param name="serviceProvider">Root service provider.</param>
        public static void SetProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets the current service provider.
        /// Throws if called before initialization.
        /// </summary>
        public static IServiceProvider Current
        {
            get
            {
                if (_serviceProvider == null)
                    throw new InvalidOperationException(
                        "ServiceLocator not initialized. Call ServiceLocator.SetProvider() during application startup.");
                return _serviceProvider;
            }
        }
    }
}
