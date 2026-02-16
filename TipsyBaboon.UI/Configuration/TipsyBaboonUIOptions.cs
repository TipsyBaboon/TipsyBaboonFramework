namespace TipsyBaboon.UI.Configuration
{
    /// <summary>
    /// Global static options controlling URL prefixes, layout defaults, and UI behavior for the TipsyBaboon UI layer.
    /// Configured at startup via <see cref="Extensions.TipsyBaboonGenericExtensions.AddTipsyBaboonGenericApis"/> and
    /// <see cref="Extensions.TipsyBaboonGenericExtensions.AddTipsyBaboonGenericPages"/>.
    /// </summary>
    public static class TipsyBaboonUIOptions
    {
        private static string _pageRoutePrefix = string.Empty;
        private static string _apiRoutePrefix = "api";
        private static string _layoutPage = "_Layout";
        private static bool _enableModalView = true;
        private static bool _autoSave = true;
        private static int _defaultPageSize = 25;
        private static bool _specifyPageAccess = false;

        /// <summary>Route prefix prepended to all generic Razor Page URLs (e.g. "admin" yields /admin/{module}/{type}).</summary>
        public static string PageRoutePrefix
        {
            get => _pageRoutePrefix;
            set => _pageRoutePrefix = value?.Trim('/') ?? string.Empty;
        }

        /// <summary>Route prefix prepended to all generic API URLs (default: "api").</summary>
        public static string ApiRoutePrefix
        {
            get => _apiRoutePrefix;
            set => _apiRoutePrefix = value?.Trim('/') ?? "api";
        }

        /// <summary>Razor layout page used by generic pages (default: "_Layout").</summary>
        public static string LayoutPage
        {
            get => _layoutPage;
            set => _layoutPage = value ?? "_Layout";
        }

        /// <summary>When true, entity detail views open in a Bootstrap modal on double-click.</summary>
        public static bool EnableModalView
        {
            get => _enableModalView;
            set => _enableModalView = value;
        }

        /// <summary>When true, field changes are persisted immediately without a Save button.</summary>
        public static bool AutoSave
        {
            get => _autoSave;
            set => _autoSave = value;
        }

        /// <summary>Number of rows per page in entity grids (default: 25, minimum: 1).</summary>
        public static int DefaultPageSize
        {
            get => _defaultPageSize;
            set => _defaultPageSize = value > 0 ? value : 25;
        }

        /// <summary>When true, pages require explicit page-access permission in addition to entity-level CRUD permissions.</summary>
        public static bool SpecifyPageAccess
        {
            get => _specifyPageAccess;
            set => _specifyPageAccess = value;
        }

        /// <summary>
        /// Builds a page URL for a module entity, optionally with an action and record ID.
        /// </summary>
        /// <param name="module">The module name (route segment).</param>
        /// <param name="entityType">The entity type name (route segment).</param>
        /// <param name="action">Optional action segment (e.g. "Create", "Detail").</param>
        /// <param name="id">Optional record identifier.</param>
        /// <returns>A relative URL such as /admin/Governance/User/Detail/abc123.</returns>
        public static string BuildPageUrl(string module, string entityType, string? action = null, string? id = null)
        {
            var prefix = string.IsNullOrEmpty(PageRoutePrefix) ? "" : "/" + PageRoutePrefix;
            var path = $"{prefix}/{module}/{entityType}";

            if (!string.IsNullOrEmpty(action))
                path += "/" + action;

            if (!string.IsNullOrEmpty(id))
                path += "/" + id;

            return path;
        }
        /// <summary>Returns the current <see cref="PageRoutePrefix"/>.</summary>
        public static string GetPagePrefix()
        {
            return PageRoutePrefix;
        }

        /// <summary>
        /// Builds an API URL for a module entity (e.g. /api/Governance/User).
        /// </summary>
        public static string BuildApiUrl(string module, string entityType)
        {
            return $"/{ApiRoutePrefix}/{module}/{entityType}";
        }
        /// <summary>Returns the current <see cref="ApiRoutePrefix"/>.</summary>
        public static string GetApiPrefix(string module, string entityType)
        {
            return ApiRoutePrefix;
        }
    }
}