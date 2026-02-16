using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Extensions;

namespace TipsyBaboon.UI.Pages.Generic
{
    /// <summary>
    /// Razor Page model for the generic entity creation view.
    /// Instantiates a new entity with defaults (including locked/pre-filled fields from query string),
    /// evaluates RBAC and ownership settings, and renders the standard create form.
    /// Supports both modal and full-page modes; handles POST to persist the new record.
    /// </summary>
    public class CreateModel : PageModel
    {
        private readonly BaseModelStore _store;
        private readonly IWebHostEnvironment _env;
        private readonly GenericPageOptions _options;
        private readonly IPermissionService _permissionService;
        private readonly ILayoutService _layoutService;

        public CreateModel(
            BaseModelStore store,
            IWebHostEnvironment env,
            GenericPageOptions options,
            IPermissionService permissionService,
            ILayoutService layoutService)
        {
            _store = store;
            _env = env;
            _options = options;
            _permissionService = permissionService;
            _layoutService = layoutService;
        }

        public string Module { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public Type? EntityType { get; set; }

        public object? Entity { get; set; }

        public SchemaDefinition? Schema
        {
            get
            {
                if (field == null && !string.IsNullOrEmpty(TypeName) && !string.IsNullOrEmpty(Module))
                    field = ModelRegistry.GetModel(TypeName, Module);
                return field;
            }
        }

        public string EntityId { get; set; } = string.Empty;

        public bool IsModal { get; set; }

        public string ApiPrefix { get; set; } = string.Empty;

        public string DetailUrlBase { get; set; } = string.Empty;

        public bool ShouldRedirectAfterCreate { get; set; }

        public string FormId { get; set; } = Guid.NewGuid().ToString();

        public string JsPrefix { get; set; } = "/_tipsybaboon/js";

        public string LayoutPage => _options.LayoutPage;

        public string[] ModelScriptPaths => Schema?.ScriptPaths ?? [];
        public string[] ModelStylePaths => Schema?.StylePaths ?? [];

        public Dictionary<string, string> LockedFields { get; set; } = new();

        public bool HasOwnership { get; set; }
        public List<OwnershipLevel> OwnershipLevels { get; set; } = new();
        public Guid? CurrentUserId { get; set; }
        public List<UserGroupInfo> UserGroups { get; set; } = new();
        public string? OwnerDisplayName { get; set; }
        public string? CurrentGroupName { get; set; }

        public async Task<IActionResult> OnGet(string module, string type)
        {
            Module = module;
            TypeName = type;

            IsModal = Request.Query.ContainsKey("isModal") || Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            var customPagePath = $"Pages/{module}/{type}/Create.cshtml";
            var fullPath = Path.Combine(_env.ContentRootPath, customPagePath);
            if (System.IO.File.Exists(fullPath))
            {
                return RedirectToPage($"/{module}/{type}/Create");
            }

            if (Schema == null)
            {
                return NotFound($"Type '{type}' not found in registered models for module '{module}'");
            }

            if (!await _permissionService.HasPageAccessForSchemaAsync(Schema))
            {
                return StatusCode(403);
            }

            var permissions = await _permissionService.GetEffectivePermissionsForSchemaAsync(Schema);
            if (!permissions.CanCreate)
            {
                return StatusCode(403);
            }

            if (IsModal)
            {
                foreach (var section in Schema.Layout)
                {
                    section.IsInitiallyCollapsed = false;
                }
            }

            EntityType = Schema.ModelType;
            if (EntityType == null)
            {
                return NotFound($"EntityType not available for '{Schema.FullyQualifiedTypeName}'");
            }

            Entity = Activator.CreateInstance(EntityType)!;
            EntityId = GetOrGenerateId(Entity, EntityType);

            PreFillFromQueryString(Entity, EntityType);

            if (Schema.ModuleName == "Governance" && Schema.TypeName == "Permission")
            {
                var pEntity = Entity as Permission;
                if (pEntity != null)
                {
                    var subModel = ModelRegistry.GetModel(pEntity.RecordId);

                    if (subModel != null && subModel.IsUIReadOnly)
                    {
                        Schema.Columns.Single(c => c.ColumnName == "CanCreate").IsUIReadOnly = true;
                        Schema.Columns.Single(c => c.ColumnName == "UpdateLevel").IsUIReadOnly = true;
                        Schema.Columns.Single(c => c.ColumnName == "DeleteLevel").IsUIReadOnly = true;

                        pEntity.CanCreate = false;
                        pEntity.UpdateLevel = PermissionLevel.None;
                        pEntity.DeleteLevel = PermissionLevel.None;
                    }
                }
            }

            await _layoutService.ApplyLayoutToSchemaAsync(Schema, Core.LayoutType.Create);

            ApiPrefix = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.ApiRoutePrefix;
            DetailUrlBase = TipsyBaboon.UI.Configuration.TipsyBaboonUIOptions.BuildPageUrl(module, type, "detail");

            // Determine if we should redirect after save based on full-screen mode
            // If Detail requires full-screen, always redirect to full-screen detail page after create
            ShouldRedirectAfterCreate = Schema.FullScreenMode == FullScreenMode.Detail || Schema.FullScreenMode == FullScreenMode.Both;

            HasOwnership = Schema.HasOwnership;
            OwnershipLevels = Schema.OwnershipLevels;
            CurrentUserId = _permissionService.CurrentUserId;

            if (HasOwnership)
            {
                if (OwnershipLevels.Contains(OwnershipLevel.Group))
                {
                    UserGroups = await _permissionService.GetCurrentUserGroupsAsync();
                }

                if (CurrentUserId != null)
                {
                    var currentUser = await _store.GetByIdAsync("Governance", "User", CurrentUserId.Value.ToString());
                    if (currentUser is User user)
                    {
                        OwnerDisplayName = user.DisplayName ?? user.UserName;
                    }
                }
            }

            return Page();
        }

        private string GetOrGenerateId(object entity, Type entityType)
        {
            if (Schema == null) return string.Empty;
            var keyCol = Schema.KeyColumns[0];
            var keyProp = entityType.GetProperty(keyCol.PropertyName);
            if (keyProp == null)
                return string.Empty;

            var currentValue = keyProp.GetValue(entity);

            if (keyProp.PropertyType == typeof(Guid))
            {
                var guidValue = (Guid)(currentValue ?? Guid.Empty);
                if (guidValue == Guid.Empty)
                {
                    guidValue = Guid.NewGuid();
                    keyProp.SetValue(entity, guidValue);
                }
                return guidValue.ToString();
            }

            if (keyProp.PropertyType == typeof(int))
            {
                return "0";
            }

            if (keyProp.PropertyType == typeof(long))
            {
                return "0";
            }

            if (keyProp.PropertyType == typeof(string))
            {
                return currentValue?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }

        private void PreFillFromQueryString(object entity, Type entityType)
        {
            foreach (var queryParam in Request.Query)
            {
                // Skip system parameters
                if (queryParam.Key.Equals("isModal", StringComparison.OrdinalIgnoreCase) ||
                    queryParam.Key.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var property = entityType.GetProperty(queryParam.Key,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase);

                if (property != null && property.CanWrite)
                {
                    try
                    {
                        var stringValue = queryParam.Value.ToString();
                        object? convertedValue = ConvertQueryStringValue(stringValue, property.PropertyType);

                        if (convertedValue != null)
                        {
                            property.SetValue(entity, convertedValue);
                            // Track this field as locked (use actual property name for case-insensitive match)
                            LockedFields[property.Name] = stringValue;
                        }
                    }
                    catch
                    {
                        // Ignore conversion errors for querystring parameters
                    }
                }
            }
        }

        private object? ConvertQueryStringValue(string value, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(Guid))
            {
                return Guid.TryParse(value, out var guid) ? guid : null;
            }
            else if (underlyingType == typeof(int))
            {
                return int.TryParse(value, out var intVal) ? intVal : null;
            }
            else if (underlyingType == typeof(long))
            {
                return long.TryParse(value, out var longVal) ? longVal : null;
            }
            else if (underlyingType == typeof(decimal))
            {
                return decimal.TryParse(value, out var decVal) ? decVal : null;
            }
            else if (underlyingType == typeof(double))
            {
                return double.TryParse(value, out var dblVal) ? dblVal : null;
            }
            else if (underlyingType == typeof(bool))
            {
                return bool.TryParse(value, out var boolVal) ? boolVal : null;
            }
            else if (underlyingType == typeof(DateTime))
            {
                return DateTime.TryParse(value, out var dateVal) ? dateVal : null;
            }
            else if (underlyingType == typeof(DateOnly))
            {
                return DateOnly.TryParse(value, out var dateOnlyVal) ? dateOnlyVal : null;
            }
            else if (underlyingType == typeof(TimeOnly))
            {
                return TimeOnly.TryParse(value, out var timeOnlyVal) ? timeOnlyVal : null;
            }
            else if (underlyingType.IsEnum)
            {
                try
                {
                    return Enum.Parse(underlyingType, value, ignoreCase: true);
                }
                catch
                {
                    return null;
                }
            }
            else if (underlyingType == typeof(string))
            {
                return value;
            }

            return null;
        }
    }
}
