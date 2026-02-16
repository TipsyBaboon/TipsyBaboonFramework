using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TipsyBaboon.Core.Configuration;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.UI.Pages.Governance.Configuration
{
    /// <summary>
    /// Razor Page model for the Configuration management page.
    /// Discovers all registered configuration types, loads their property metadata and current values,
    /// and renders a two-pane editor (type selector + property grid).
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly IConfigurationPageService _configService;
        private readonly IPermissionService _permissionService;

        public IndexModel(IConfigurationPageService configService, IPermissionService permissionService)
        {
            _configService = configService;
            _permissionService = permissionService;
        }

        public List<Type> ConfigurationTypes { get; set; } = new();
        public string? SelectedTypeName { get; set; }
        public List<ConfigurationPropertyInfo> ConfigurationProperties { get; set; } = new();
        public SchemaDefinition? ConfigurationSchema { get; set; }
        public Dictionary<string, object?> ConfigurationValues { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string? type)
        {
            if (!await _permissionService.CanManageConfigurationAsync())
                return Forbid();

            ConfigurationTypes = _configService.DiscoverConfigurationTypes();

            SelectedTypeName = type ?? ConfigurationTypes.FirstOrDefault()?.Name;

            if (!string.IsNullOrEmpty(SelectedTypeName))
            {
                var selectedType = ConfigurationTypes.FirstOrDefault(t => t.Name == SelectedTypeName);
                if (selectedType != null)
                {
                    ConfigurationProperties = await LoadPropertiesAsync(selectedType);
                    // Use discovered schema from ModelRegistry instead of synthetic
                    ConfigurationSchema = ModelRegistry.GetModel(selectedType)
                        ?? CreateSchemaFromProperties(ConfigurationProperties, SelectedTypeName);
                    ConfigurationValues = CreateValuesFromProperties(ConfigurationProperties);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync([FromBody] SaveRequest request)
        {
            if (!await _permissionService.CanManageConfigurationAsync())
                return Forbid();

            try
            {
                if (string.IsNullOrEmpty(request.TypeName) || string.IsNullOrEmpty(request.PropertyName))
                {
                    return new JsonResult(new { success = false, error = "Type and property name are required" });
                }

                var configTypes = _configService.DiscoverConfigurationTypes();
                var selectedType = configTypes.FirstOrDefault(t => t.Name == request.TypeName);

                if (selectedType == null)
                {
                    return new JsonResult(new { success = false, error = "Invalid configuration type" });
                }

                var property = selectedType.GetProperty(request.PropertyName);
                if (property == null)
                {
                    return new JsonResult(new { success = false, error = "Property not found" });
                }

                object? convertedValue = ConvertValue(request.Value, property.PropertyType);

                await SavePropertyAsync(selectedType, request.PropertyName, convertedValue);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        private async Task<List<ConfigurationPropertyInfo>> LoadPropertiesAsync(Type configurationType)
        {
            var method = typeof(IConfigurationPageService)
                .GetMethod(nameof(IConfigurationPageService.GetConfigurationPropertiesAsync))!
                .MakeGenericMethod(configurationType);

            var task = (Task<List<ConfigurationPropertyInfo>>)method.Invoke(_configService, new object[] { default(CancellationToken) })!;
            return await task;
        }

        private SchemaDefinition CreateSchemaFromProperties(List<ConfigurationPropertyInfo> properties, string typeName)
        {
            var schema = new SchemaDefinition
            {
                TypeName = typeName,
                FullyQualifiedTypeName = $"TipsyBaboon.Configuration.{typeName}",
                ModuleName = "Configuration",
                PluralName = typeName,
                IsReadOnly = false,
                Columns = new List<ColumnDefinition>(),
                Layout = new List<LayoutSection>()
            };

            var groups = properties.GroupBy(p => p.GroupName).OrderBy(g => g.Min(p => p.Order));

            foreach (var group in groups)
            {
                var section = new LayoutSection
                {
                    Title = group.Key ?? "General",
                    Properties = new List<LayoutProperty>()
                };

                foreach (var property in group.OrderBy(p => p.Order))
                {
                    var column = new ColumnDefinition
                    {
                        PropertyName = property.Name,
                        DisplayName = property.DisplayName ?? property.Name,
                        Description = property.Description ?? string.Empty,
                        ClrType = property.PropertyType,
                        ClrTypeName = property.PropertyType.Name,
                        ShowInForm = true,
                        ShowLabel = true,
                        IsRequired = false,
                        IsReadOnly = false,
                        GroupName = group.Key ?? "General",
                        DisplayOrder = property.Order,
                        ColumnSize = 12
                    };

                    schema.Columns.Add(column);
                    section.Properties.Add(new LayoutProperty
                    {
                        PropertyName = property.Name,
                        ColumnSize = 12,
                        ShowLabel = true
                    });
                }

                schema.Layout.Add(section);
            }

            return schema;
        }

        private Dictionary<string, object?> CreateValuesFromProperties(List<ConfigurationPropertyInfo> properties)
        {
            var values = new Dictionary<string, object?>();
            foreach (var property in properties)
            {
                values[property.Name] = property.Value;
            }
            return values;
        }

        private async Task SavePropertyAsync(Type configurationType, string propertyName, object? value)
        {
            var method = typeof(IConfigurationPageService)
                .GetMethod(nameof(IConfigurationPageService.SaveConfigurationPropertyAsync))!
                .MakeGenericMethod(configurationType);

            var task = (Task)method.Invoke(_configService, new object?[] { propertyName, value, 100, default(CancellationToken) })!;
            await task;
        }

        private object? ConvertValue(string? value, Type targetType)
        {
            if (string.IsNullOrEmpty(value))
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(bool))
            {
                return bool.Parse(value);
            }
            if (underlyingType == typeof(int))
            {
                return int.Parse(value);
            }
            if (underlyingType == typeof(long))
            {
                return long.Parse(value);
            }
            if (underlyingType == typeof(decimal))
            {
                return decimal.Parse(value);
            }
            if (underlyingType == typeof(double))
            {
                return double.Parse(value);
            }
            if (underlyingType.IsEnum)
            {
                return Enum.Parse(underlyingType, value);
            }

            return value;
        }

        public class SaveRequest
        {
            public string? TypeName { get; set; }
            public string? PropertyName { get; set; }
            public string? Value { get; set; }
        }
    }
}
