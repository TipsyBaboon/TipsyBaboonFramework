using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Configuration;
using TipsyBaboon.UI.Services;
using IAppAuthenticationService = TipsyBaboon.Core.Interfaces.IAuthenticationService;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account
{
    public class ExternalLoginModel : IdentityPageModel
    {
        private readonly IAppAuthenticationService _authService;
        private readonly UserGovernanceService _governanceService;

        public ExternalLoginModel(
            IAppAuthenticationService authService,
            TipsyBaboonIdentityPageOptions options,
            UserGovernanceService governanceService) : base(options)
        {
            _authService = authService;
            _governanceService = governanceService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = default!;

        public string? ProviderDisplayName { get; set; }

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = default!;
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (info?.Principal == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var nameIdentifierClaim = info.Principal.FindFirst(ClaimTypes.NameIdentifier);
            var emailClaim = info.Principal.FindFirst(ClaimTypes.Email);
            var nameClaim = info.Principal.FindFirst(ClaimTypes.Name);

            if (nameIdentifierClaim == null)
            {
                ErrorMessage = "Error: No user identifier from external provider.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var providerKey = nameIdentifierClaim.Value;
            var loginProvider = info.Principal.Identity?.AuthenticationType ?? "Unknown";

            var userLogin = await _authService.FindUserLoginAsync(loginProvider, providerKey);

            if (userLogin != null)
            {
                var user = await _authService.FindByIdAsync(userLogin.UserId);
                if (user != null)
                {
                    await SignInUserAsync(user, returnUrl);
                    return LocalRedirect(returnUrl);
                }
            }

            if (emailClaim != null && !string.IsNullOrEmpty(emailClaim.Value))
            {
                var existingUser = await _authService.FindByEmailAsync(emailClaim.Value);
                if (existingUser != null)
                {
                    await _authService.AddUserLoginAsync(existingUser.Id, loginProvider, providerKey, loginProvider);
                    await SignInUserAsync(existingUser, returnUrl);
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    var newUser = await _authService.CreateUserAsync(
                        emailClaim.Value,
                        nameClaim?.Value ?? emailClaim.Value,
                        password: null);

                    await _governanceService.AssignFirstUserAdminIfNeededAsync(newUser);
                    await _authService.AddUserLoginAsync(newUser.Id, loginProvider, providerKey, loginProvider);
                    await SignInUserAsync(newUser, returnUrl);
                    return LocalRedirect(returnUrl);
                }
            }

            ReturnUrl = returnUrl;
            ProviderDisplayName = loginProvider;

            TempData["ExternalLoginProvider"] = loginProvider;
            TempData["ExternalLoginProviderKey"] = providerKey;
            TempData["ExternalLoginDisplayName"] = nameClaim?.Value;

            if (emailClaim != null)
            {
                Input = new InputModel { Email = emailClaim.Value };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var loginProvider = TempData["ExternalLoginProvider"] as string;
            var providerKey = TempData["ExternalLoginProviderKey"] as string;
            var displayName = TempData["ExternalLoginDisplayName"] as string;

            if (loginProvider == null || providerKey == null)
            {
                ErrorMessage = "Error loading external login information on confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var existingUser = await _authService.FindByEmailAsync(Input.Email);
                if (existingUser != null)
                {
                    await _authService.AddUserLoginAsync(existingUser.Id, loginProvider, providerKey, loginProvider);
                    await SignInUserAsync(existingUser, returnUrl);
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    var newUser = await _authService.CreateUserAsync(
                        Input.Email,
                        displayName ?? Input.Email,
                        password: null);

                    await _governanceService.AssignFirstUserAdminIfNeededAsync(newUser);
                    await _authService.AddUserLoginAsync(newUser.Id, loginProvider, providerKey, loginProvider);
                    await SignInUserAsync(newUser, returnUrl);
                    return LocalRedirect(returnUrl);
                }
            }

            ProviderDisplayName = loginProvider;
            ReturnUrl = returnUrl;
            return Page();
        }

        private async Task SignInUserAsync(User user, string returnUrl)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName ?? user.UserName ?? user.Email ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }
}
