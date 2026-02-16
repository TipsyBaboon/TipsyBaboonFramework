using Microsoft.Extensions.Caching.Memory;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;
using TipsyBaboon.UI.Models;

namespace TipsyBaboon.UI.Services
{
    /// <summary>
    /// Builds the application navigation menu from registered model metadata.
    /// Groups models by their <see cref="SchemaDefinition.GroupName"/>, applies menu ordering,
    /// and filters items based on the current user's RBAC permissions.
    /// Results are cached in-memory for one hour.
    /// </summary>
    public class MenuBuilder
    {
        private readonly IMemoryCache _cache;
        private const string MenuCacheKey = "TipsyBaboon.UI.Menu";
        private const int CacheExpirationMinutes = 60;

        public MenuBuilder(IMemoryCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Returns the full menu from cache or generates it from model registry metadata.
        /// </summary>
        public Menu GetMenu(UIConfiguration? config = null)
        {
            config ??= UIConfiguration.Instance;

            return _cache.GetOrCreate(MenuCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes);
                return GenerateMenu(config);
            })!;
        }

        /// <summary>
        /// Returns the menu filtered by the current user's permissions.
        /// Removes groups and items the user lacks read access to.
        /// </summary>
        public async Task<Menu> GetFilteredMenuAsync(IPermissionService permissionService, UIConfiguration? config = null, CancellationToken cancellationToken = default)
        {
            var baseMenu = GetMenu(config);
            var canManageConfiguration = await permissionService.CanManageConfigurationAsync(cancellationToken);

            var radModuleSchema = ModelRegistry.GetModel("RADModule", "Governance");
            var canViewChangeLog = radModuleSchema != null &&
                await permissionService.HasPrivilegeAsync(radModuleSchema.RecordModelId, "View Change Log", cancellationToken);

            var externalLinks = await permissionService.GetAccessibleExternalLinksAsync(cancellationToken);

            var filteredItems = new List<MenuItem>();
            foreach (var item in baseMenu.RootItems)
            {
                var filteredItem = await FilterMenuItemAsync(item, permissionService, canManageConfiguration, canViewChangeLog, cancellationToken);
                if (filteredItem != null)
                    filteredItems.Add(filteredItem);
            }

            if (externalLinks.Any())
            {
                var externalsGroup = new MenuItem
                {
                    Label = "Externals",
                    Type = MenuItemType.Group,
                    Icon = "bi bi-box-arrow-up-right",
                    Order = 10000
                };

                foreach (var link in externalLinks)
                {
                    externalsGroup.Children.Add(new MenuItem
                    {
                        Label = link.Title,
                        Type = MenuItemType.CustomLink,
                        Url = link.Url,
                        Icon = link.Icon ?? "bi bi-link-45deg",
                        Target = link.OpenInNewTab ? "_blank" : null,
                        Tooltip = link.Description,
                        Order = link.MenuOrder
                    });
                }

                filteredItems.Add(externalsGroup);
            }

            return new Menu
            {
                RootItems = filteredItems,
                LastGenerated = baseMenu.LastGenerated
            };
        }

        private async Task<MenuItem?> FilterMenuItemAsync(MenuItem item, IPermissionService permissionService, bool canManageConfiguration, bool canViewChangeLog, CancellationToken cancellationToken)
        {
            if (!item.IsVisible)
                return null;

            if (!string.IsNullOrEmpty(item.PermissionRequired))
            {
                if (item.PermissionRequired == "Privilege:Configuration.Manage")
                {
                    if (!canManageConfiguration)
                        return null;
                }
                else if (item.PermissionRequired == "Privilege:ChangeLog.View")
                {
                    if (!canViewChangeLog)
                        return null;
                }
                else if (item.Type == MenuItemType.ModelLink && !string.IsNullOrEmpty(item.ModelTypeName))
                {
                    var schema = ModelRegistry.GetModel(item.ModelTypeName);
                    if (schema != null)
                    {
                        var permissions = await permissionService.GetEffectivePermissionsForSchemaAsync(schema, cancellationToken);
                        if (!permissions.CanRead)
                            return null;
                    }
                }
            }

            if (item.Children.Count > 0)
            {
                var filteredChildren = new List<MenuItem>();
                foreach (var child in item.Children)
                {
                    var filteredChild = await FilterMenuItemAsync(child, permissionService, canManageConfiguration, canViewChangeLog, cancellationToken);
                    if (filteredChild != null)
                        filteredChildren.Add(filteredChild);
                }

                if (item.Type == MenuItemType.Group && filteredChildren.Count == 0)
                    return null;

                return new MenuItem
                {
                    Label = item.Label,
                    Icon = item.Icon,
                    Type = item.Type,
                    Url = item.Url,
                    ModelTypeName = item.ModelTypeName,
                    PermissionRequired = item.PermissionRequired,
                    Order = item.Order,
                    Children = filteredChildren,
                    IsVisible = item.IsVisible,
                    Target = item.Target,
                    CssClass = item.CssClass,
                    Tooltip = item.Tooltip
                };
            }

            return item;
        }

        public Menu GenerateMenu(UIConfiguration config)
        {
            var menu = new Menu
            {
                LastGenerated = DateTime.UtcNow
            };

            var discoveredSchemas = ModelRegistry.GetModelsForMenu();
            var rootItems = new List<MenuItem>();

            rootItems.Add(new MenuItem
            {
                Label = "Home",
                Type = MenuItemType.CustomLink,
                Url = "/",
                Icon = "bi bi-house",
                Order = -1000
            });

            var groupedItems = GroupSchemasByGroupName(discoveredSchemas.ToList());
            rootItems.AddRange(groupedItems);

            var governanceGroup = rootItems.FirstOrDefault(i => i.Label == "Governance");
            if (governanceGroup != null)
            {
                governanceGroup.Children.Add(new MenuItem
                {
                    Label = "Configuration",
                    Type = MenuItemType.CustomLink,
                    Url = TipsyBaboonUIOptions.BuildPageUrl("Governance", "Configuration"),
                    Icon = "bi bi-gear",
                    PermissionRequired = "Privilege:Configuration.Manage",
                    Order = 80
                });
                governanceGroup.Children.Add(new MenuItem
                {
                    Label = "Change Log",
                    Icon = "bi bi-clock-history",
                    Url = TipsyBaboonUIOptions.BuildPageUrl("Governance", "ChangeLog"),
                    Order = 100,
                    PermissionRequired = "Privilege:ChangeLog.View"
                });
                governanceGroup.Children = governanceGroup.Children.OrderBy(x => x.Order).ThenBy(x => x.Label).ToList();
            }

            menu.RootItems = rootItems.OrderBy(g => g.Label == "Governance").ThenBy(x => x.Order).ThenBy(x => x.Label).ToList();
            return menu;
        }

        private List<MenuItem> GroupSchemasByGroupName(List<SchemaDefinition> schemas)
        {
            var groups = new Dictionary<string, MenuItem>();
            var ungroupedItems = new List<MenuItem>();

            foreach (var schema in schemas)
            {
                var groupName = schema.GroupName;
                var shortModule = schema.ModuleName.Split('.').Last();

                var modelItem = new MenuItem
                {
                    Label = schema.MenuLabel ?? schema.PluralName ?? schema.TypeName,
                    Type = MenuItemType.ModelLink,
                    ModelTypeName = schema.FullyQualifiedTypeName,
                    Url = $"/{TipsyBaboonUIOptions.PageRoutePrefix}/{shortModule}/{schema.TypeName}",
                    Icon = schema.MenuIcon ?? "bi bi-file-earmark",
                    PermissionRequired = $"{schema.TypeName}.View",
                    Order = schema.MenuOrder
                };

                if (string.IsNullOrWhiteSpace(groupName))
                {
                    ungroupedItems.Add(modelItem);
                }
                else
                {
                    if (!groups.ContainsKey(groupName))
                    {
                        groups[groupName] = new MenuItem
                        {
                            Label = groupName,
                            Type = MenuItemType.Group,
                            Icon = GetGroupIcon(groupName),
                            Order = GetGroupOrder(groupName)
                        };
                    }

                    groups[groupName].Children.Add(modelItem);
                }
            }

            foreach (var group in groups.Values)
            {
                group.Children = group.Children.OrderBy(x => x.Order).ThenBy(x => x.Label).ToList();
            }

            var result = new List<MenuItem>();
            result.AddRange(groups.Values);
            result.AddRange(ungroupedItems);
            return result;
        }

        private string? GetGroupIcon(string groupName)
        {
            return groupName.ToLower() switch
            {
                "governance" => "bi bi-shield-lock",
                "identity" or "user" or "users" => "bi bi-people",
                "security" => "bi bi-shield",
                "crm" => "bi bi-person-lines-fill",
                "blog" => "bi bi-journal-text",
                "reports" => "bi bi-bar-chart",
                "admin" or "administration" => "bi bi-tools",
                "fishing" => "bi bi-water",
                _ => "bi bi-folder"
            };
        }

        private int GetGroupOrder(string groupName)
        {
            return groupName.ToLower() switch
            {
                "governance" => 100,
                "identity" or "user" or "users" => 200,
                "security" => 300,
                "crm" => 400,
                "fishing" => 500,
                "blog" => 600,
                "reports" => 800,
                "admin" or "administration" => 900,
                _ => 700
            };
        }

        public void ClearCache()
        {
            _cache.Remove(MenuCacheKey);
        }
    }
}
