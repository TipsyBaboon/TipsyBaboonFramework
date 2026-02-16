using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Security.Claims;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account.Manage;

public class PreferencesModel : IdentityPageModel
{
    private readonly IUserPreferenceService _preferenceService;

    public PreferencesModel(IUserPreferenceService preferenceService, TipsyBaboonIdentityPageOptions options) : base(options)
    {
        _preferenceService = preferenceService;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public List<Type> PreferenceTypes { get; set; } = new();
    public string? SelectedTypeName { get; set; }
    public SchemaDefinition? PreferenceSchema { get; set; }
    public Dictionary<string, object?> PreferenceValues { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? type)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return NotFound("Unable to load user.");

        PreferenceTypes = _preferenceService.DiscoverUserPreferenceTypes();
        SelectedTypeName = type ?? PreferenceTypes.FirstOrDefault()?.Name;

        if (!string.IsNullOrEmpty(SelectedTypeName))
        {
            var selectedType = PreferenceTypes.FirstOrDefault(t => t.Name == SelectedTypeName);
            if (selectedType != null)
            {
                PreferenceSchema = ModelRegistry.GetModel(selectedType);
                PreferenceValues = await LoadPreferenceValuesAsync(selectedType, userId.Value);
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync([FromBody] SaveRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            if (string.IsNullOrEmpty(request.TypeName) || string.IsNullOrEmpty(request.PropertyName))
            {
                return new JsonResult(new { success = false, error = "Type and property name are required" });
            }

            var prefTypes = _preferenceService.DiscoverUserPreferenceTypes();
            var selectedType = prefTypes.FirstOrDefault(t => t.Name == request.TypeName);

            if (selectedType == null)
            {
                return new JsonResult(new { success = false, error = "Invalid preference type" });
            }

            var property = selectedType.GetProperty(request.PropertyName);
            if (property == null)
            {
                return new JsonResult(new { success = false, error = "Property not found" });
            }

            object? convertedValue = ConvertValue(request.Value, property.PropertyType);

            await SavePreferencePropertyAsync(selectedType, userId.Value, request.PropertyName, convertedValue);

            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    private async Task<Dictionary<string, object?>> LoadPreferenceValuesAsync(Type preferenceType, Guid userId)
    {
        var values = new Dictionary<string, object?>();

        var loadMethod = typeof(IUserPreferenceService)
            .GetMethod(nameof(IUserPreferenceService.LoadUserPreferencesAsync))!
            .MakeGenericMethod(preferenceType);

        var task = (Task)loadMethod.Invoke(_preferenceService, new object[] { userId, default(CancellationToken) })!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result")!;
        var instance = resultProperty.GetValue(task);

        if (instance != null)
        {
            var properties = preferenceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(UserPreferenceBase.ModuleName) && p.CanRead);

            foreach (var prop in properties)
            {
                values[prop.Name] = prop.GetValue(instance);
            }
        }

        return values;
    }

    private async Task SavePreferencePropertyAsync(Type preferenceType, Guid userId, string propertyName, object? value)
    {
        var method = typeof(IUserPreferenceService)
            .GetMethod(nameof(IUserPreferenceService.SaveUserPreferencePropertyAsync))!
            .MakeGenericMethod(preferenceType);

        var task = (Task)method.Invoke(_preferenceService, new object?[] { userId, propertyName, value, default(CancellationToken) })!;
        await task;
    }

    private object? ConvertValue(string? value, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(bool))
            return bool.Parse(value);
        if (underlyingType == typeof(int))
            return int.Parse(value);
        if (underlyingType == typeof(long))
            return long.Parse(value);
        if (underlyingType == typeof(decimal))
            return decimal.Parse(value);
        if (underlyingType == typeof(double))
            return double.Parse(value);
        if (underlyingType.IsEnum)
            return Enum.Parse(underlyingType, value);

        return value;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }

    public class SaveRequest
    {
        public string? TypeName { get; set; }
        public string? PropertyName { get; set; }
        public string? Value { get; set; }
    }
}
