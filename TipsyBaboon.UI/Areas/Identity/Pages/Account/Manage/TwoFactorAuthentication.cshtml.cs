using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.ComponentModel.DataAnnotations;
using System.Text;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account.Manage;

public class TwoFactorAuthenticationModel : IdentityPageModel
{
    private readonly IAuthenticationService _authenticationService;

    public TwoFactorAuthenticationModel(IAuthenticationService authenticationService, TipsyBaboonIdentityPageOptions options) : base(options)
    {
        _authenticationService = authenticationService;
    }

    public bool IsTwoFactorEnabled { get; set; }
    public bool ShowSetup { get; set; }
    public string? FormattedSharedKey { get; set; }
    public string? QrCodeBase64 { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public string? SharedKey { get; set; }

    [BindProperty]
    [Required]
    [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Text)]
    [Display(Name = "Verification Code")]
    public string? VerificationCode { get; set; }

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

        IsTwoFactorEnabled = user.TwoFactorEnabled;
        return Page();
    }

    public async Task<IActionResult> OnPostSetupAsync()
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

        var unformattedKey = await _authenticationService.GenerateAuthenticatorKeyAsync();
        SharedKey = unformattedKey;
        FormattedSharedKey = FormatKey(unformattedKey);
        QrCodeBase64 = GenerateQrCodeBase64(user.Email ?? "user", unformattedKey);
        ShowSetup = true;
        IsTwoFactorEnabled = false;

        return Page();
    }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return NotFound("Unable to load user.");
        }

        if (!ModelState.IsValid || string.IsNullOrEmpty(SharedKey) || string.IsNullOrEmpty(VerificationCode))
        {
            var user = await _authenticationService.FindByIdAsync(userId);
            if (user != null)
            {
                FormattedSharedKey = FormatKey(SharedKey ?? "");
                QrCodeBase64 = GenerateQrCodeBase64(user.Email ?? "user", SharedKey ?? "");
                ShowSetup = true;
            }
            return Page();
        }

        var verificationCode = VerificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2faTokenValid = await _authenticationService.EnableTwoFactorAsync(userId, SharedKey, verificationCode);

        if (!is2faTokenValid)
        {
            ModelState.AddModelError(nameof(VerificationCode), "Verification code is invalid.");
            var user = await _authenticationService.FindByIdAsync(userId);
            if (user != null)
            {
                FormattedSharedKey = FormatKey(SharedKey);
                QrCodeBase64 = GenerateQrCodeBase64(user.Email ?? "user", SharedKey);
                ShowSetup = true;
            }
            return Page();
        }

        StatusMessage = "Your authenticator app has been verified and 2FA is now enabled.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return NotFound("Unable to load user.");
        }

        var result = await _authenticationService.DisableTwoFactorAsync(userId);
        if (!result)
        {
            StatusMessage = "Error: Could not disable 2FA.";
            return RedirectToPage();
        }

        StatusMessage = "Two-factor authentication has been disabled.";
        return RedirectToPage();
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeBase64(string email, string unformattedKey)
    {
        var authenticatorUri = string.Format(
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            Uri.EscapeDataString("TipsyBaboon"),
            Uri.EscapeDataString(email),
            unformattedKey);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(authenticatorUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(5);
        return Convert.ToBase64String(qrCodeBytes);
    }
}
