using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Extensions
{
    /// <summary>
    /// Extension methods for registering generic API controllers and Razor Pages
    /// that provide automatic CRUD UI and REST endpoints for all registered models.
    /// </summary>
    public static class TipsyBaboonGenericExtensions
    {
        /// <summary>
        /// Registers the generic API controller with JSON serialization settings and route conventions.
        /// Provides REST endpoints at <c>{routePrefix}/{module}/{model}</c> for all registered model types.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="routePrefix">API route prefix (default: "api").</param>
        /// <param name="maxBatchSize">Maximum batch size for bulk operations.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTipsyBaboonGenericApis(
            this IServiceCollection services,
            string routePrefix = "api",
            int maxBatchSize = 1000)
        {
            TipsyBaboonUIOptions.ApiRoutePrefix = routePrefix;

            services.AddControllers(options =>
                {
                    options.Conventions.Add(new GenericApiRouteConvention());
                    options.Conventions.Add(new ConfigurationApiRouteConvention());
                    options.Conventions.Add(new UserPreferenceApiRouteConvention());
                })
                .AddJsonOptions(opts =>
                {
                    var defaultOpts = TipsyBaboonJsonOptions.Default;
                    opts.JsonSerializerOptions.PropertyNamingPolicy = defaultOpts.PropertyNamingPolicy;
                    opts.JsonSerializerOptions.DefaultIgnoreCondition = defaultOpts.DefaultIgnoreCondition;
                    opts.JsonSerializerOptions.PropertyNameCaseInsensitive = defaultOpts.PropertyNameCaseInsensitive;
                    opts.JsonSerializerOptions.ReferenceHandler = defaultOpts.ReferenceHandler;
                    opts.JsonSerializerOptions.MaxDepth = defaultOpts.MaxDepth;
                    opts.JsonSerializerOptions.NumberHandling = defaultOpts.NumberHandling;
                    foreach (var converter in defaultOpts.Converters)
                    {
                        opts.JsonSerializerOptions.Converters.Add(converter);
                    }
                });

            services.AddSingleton(new GenericApiOptions
            {
                RoutePrefix = routePrefix,
                MaxBatchSize = maxBatchSize
            });

            return services;
        }

        /// <summary>
        /// Registers generic Razor Pages for list, detail, and create views.
        /// Provides automatic CRUD pages at <c>{routePrefix}/{module}/{model}</c> for all registered model types.
        /// Includes optional configuration management pages.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="routePrefix">Page route prefix (default: none).</param>
        /// <param name="layoutPage">Razor layout page path for all generic pages.</param>
        /// <param name="includeConfigPages">Whether to include configuration management pages.</param>
        /// <param name="enableModalView">Whether detail views open in modals vs. full-screen.</param>
        /// <param name="autoSave">Whether forms auto-save on field change.</param>
        /// <param name="defaultPageSize">Default grid page size.</param>
        /// <param name="specifyPageAccess">Whether UsePages permission check is enforced.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTipsyBaboonGenericPages(
            this IServiceCollection services,
            string? routePrefix = null,
            string layoutPage = "/Pages/Shared/_Layout.cshtml",
            bool includeConfigPages = true,
            bool enableModalView = true,
            bool autoSave = true,
            int defaultPageSize = 25,
            bool specifyPageAccess = false)
        {
            TipsyBaboonUIOptions.PageRoutePrefix = routePrefix ?? string.Empty;
            TipsyBaboonUIOptions.LayoutPage = layoutPage;
            TipsyBaboonUIOptions.EnableModalView = enableModalView;
            TipsyBaboonUIOptions.AutoSave = autoSave;
            TipsyBaboonUIOptions.DefaultPageSize = defaultPageSize;
            TipsyBaboonUIOptions.SpecifyPageAccess = specifyPageAccess;

            services.AddRazorPages(options =>
            {
                options.Conventions.Add(new GenericPageRouteConvention());
            });

            services.AddSingleton(new GenericPageOptions
            {
                RoutePrefix = routePrefix,
                LayoutPage = layoutPage,
                IncludeConfigPages = includeConfigPages,
                EnableModalView = enableModalView,
                AutoSave = autoSave,
                SpecifyPageAccess = specifyPageAccess
            });

            return services;
        }

        /// <summary>
        /// Configures the middleware pipeline to serve embedded static resources (JS, CSS) from the TipsyBaboon.UI assembly.
        /// These resources power the generic grid, detail, and form rendering.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="requestPath">URL prefix for embedded resources (default: "/_tipsybaboon").</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseTipsyBaboonEmbeddedResources(
            this IApplicationBuilder app,
            string requestPath = "/_tipsybaboon")
        {
            var assembly = typeof(TipsyBaboonGenericExtensions).Assembly;

            var fileProvider = new ManifestEmbeddedFileProvider(assembly, "wwwroot");

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = requestPath,
                ContentTypeProvider = new FileExtensionContentTypeProvider()
            });

            return app;
        }
    }

    /// <summary>
    /// Configuration for the generic API controller routes and batch limits.
    /// </summary>
    public class GenericApiOptions
    {
        /// <summary>API route prefix (e.g., "api" yields routes like <c>/api/Governance/User</c>).</summary>
        public string RoutePrefix { get; set; } = "api";

        /// <summary>Maximum number of records allowed in a single batch request.</summary>
        public int MaxBatchSize { get; set; } = 100;
    }

    /// <summary>
    /// Configuration for generic Razor Page routes and UI behavior.
    /// </summary>
    public class GenericPageOptions
    {
        /// <summary>Optional route prefix for all generic pages.</summary>
        public string? RoutePrefix { get; set; }

        /// <summary>Razor layout page path applied to all generic pages.</summary>
        public string LayoutPage { get; set; } = "/Pages/Shared/_Layout.cshtml";

        /// <summary>Whether to include the configuration management admin pages.</summary>
        public bool IncludeConfigPages { get; set; } = true;

        /// <summary>Whether detail views open as modals rather than full pages.</summary>
        public bool EnableModalView { get; set; } = true;

        /// <summary>Whether forms auto-save on field change without explicit submit.</summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>Whether UsePages permission flag is enforced for page access.</summary>
        public bool SpecifyPageAccess { get; set; } = false;
    }
}
