using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account.Manage;

public class ChangePasswordModel : IdentityPageModel
{
    private readonly IAuthenticationService _authenticationService;

    public ChangePasswordModel(IAuthenticationService authenticationService, TipsyBaboonIdentityPageOptions options) : base(options)
    {
        _authenticationService = authenticationService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return NotFound("Unable to load user.");
        }

        var user = await _authenticationService.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userId}'.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return NotFound("Unable to load user.");
        }

        var changePasswordResult = await _authenticationService.ChangePasswordAsync(userId, Input.OldPassword, Input.NewPassword);
        if (!changePasswordResult)
        {
            ModelState.AddModelError(string.Empty, "Error changing password. Please verify your current password is correct.");
            return Page();
        }

        StatusMessage = "Your password has been changed.";
        return RedirectToPage();
    }
}
