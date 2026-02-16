using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.UI.Controllers
{
    /// <summary>
    /// API controller for reading and writing per-user preference key-value pairs.
    /// Preferences are scoped to the authenticated user and stored via <see cref="IUserPreferenceService"/>.
    /// Route is dynamically set by <see cref="Configuration.UserPreferenceApiRouteConvention"/>.
    /// </summary>
    [ApiController]
    [Authorize]
    public class UserPreferenceApiController : ControllerBase
    {
        private readonly IUserPreferenceService _preferenceService;

        public UserPreferenceApiController(IUserPreferenceService preferenceService)
        {
            _preferenceService = preferenceService;
        }

        /// <summary>
        /// Retrieves a single user preference by key for the authenticated user.
        /// </summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetPreference(string key, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var value = await _preferenceService.GetPreferenceAsync(userId.Value, key, cancellationToken);

            return Ok(new { key, value = value ?? string.Empty });
        }

        /// <summary>
        /// Stores or updates a single user preference by key for the authenticated user.
        /// </summary>
        [HttpPut("")]
        public async Task<IActionResult> SetPreference([FromBody] SetPreferenceRequest request, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            await _preferenceService.SetPreferenceAsync(userId.Value, request.Key, request.Value, cancellationToken);

            return Ok(new { success = true });
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return null;
            return userId;
        }

        /// <summary>
        /// Request body for setting a user preference.
        /// </summary>
        public class SetPreferenceRequest
        {
            public required string Key { get; set; }
            public string? Value { get; set; }
        }
    }
}
