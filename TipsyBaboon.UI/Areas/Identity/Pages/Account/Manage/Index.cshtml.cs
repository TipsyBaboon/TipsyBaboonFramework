using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account.Manage;

public class IndexModel : IdentityPageModel
{
    private readonly IAuthenticationService _authenticationService;

    public IndexModel(IAuthenticationService authenticationService, TipsyBaboonIdentityPageOptions options) : base(options)
    {
        _authenticationService = authenticationService;
    }

    public string? Email { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Display(Name = "Display Name")]
        [StringLength(100, ErrorMessage = "The {0} must be at most {1} characters long.")]
        public string? DisplayName { get; set; }
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

        Email = user.Email;
        Input = new InputModel
        {
            DisplayName = user.DisplayName
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
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

        if (!ModelState.IsValid)
        {
            Email = user.Email;
            return Page();
        }

        if (Input.DisplayName != user.DisplayName)
        {
            await _authenticationService.UpdateDisplayNameAsync(userId, Input.DisplayName);
        }

        StatusMessage = "Your profile has been updated";
        return RedirectToPage();
    }
}
