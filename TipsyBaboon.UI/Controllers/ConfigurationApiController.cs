using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TipsyBaboon.Core.Configuration;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.UI.Controllers
{
    /// <summary>
    /// API controller for saving module configuration property values.
    /// Route is dynamically set by <see cref="Configuration.ConfigurationApiRouteConvention"/>.
    /// Requires the "Manage Configuration" privilege.
    /// </summary>
    [ApiController]
    [Authorize]
    public class ConfigurationApiController : ControllerBase
    {
        private readonly IConfigurationPageService _configService;
        private readonly IPermissionService _permissionService;

        public ConfigurationApiController(
            IConfigurationPageService configService,
            IPermissionService permissionService)
        {
            _configService = configService;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Saves a single configuration property value. Discovers the configuration type by name,
        /// converts the string value to the property's CLR type, and persists it.
        /// </summary>
        [HttpPost("save")]
        public async Task<IActionResult> SaveProperty([FromBody] SavePropertyRequest request, CancellationToken cancellationToken)
        {
            if (!await _permissionService.CanManageConfigurationAsync())
                return Forbid();

            try
            {
                if (string.IsNullOrEmpty(request.TypeName) || string.IsNullOrEmpty(request.PropertyName))
                {
                    return BadRequest(new { success = false, error = "Type and property name are required" });
                }

                var configTypes = _configService.DiscoverConfigurationTypes();
                var selectedType = configTypes.FirstOrDefault(t => t.Name == request.TypeName);

                if (selectedType == null)
                {
                    return BadRequest(new { success = false, error = "Invalid configuration type" });
                }

                var property = selectedType.GetProperty(request.PropertyName);
                if (property == null)
                {
                    return BadRequest(new { success = false, error = "Property not found" });
                }

                object? convertedValue = ConvertValue(request.Value, property.PropertyType);

                await SavePropertyAsync(selectedType, request.PropertyName, convertedValue, cancellationToken);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private async Task SavePropertyAsync(Type configurationType, string propertyName, object? value, CancellationToken cancellationToken)
        {
            var method = typeof(IConfigurationPageService)
                .GetMethod(nameof(IConfigurationPageService.SaveConfigurationPropertyAsync))!
                .MakeGenericMethod(configurationType);

            var task = (Task)method.Invoke(_configService, new object?[] { propertyName, value, 100, cancellationToken })!;
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

        /// <summary>
        /// Request body for saving a single configuration property.
        /// </summary>
        public class SavePropertyRequest
        {
            public string? TypeName { get; set; }
            public string? PropertyName { get; set; }
            public string? Value { get; set; }
        }
    }
}
