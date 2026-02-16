using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;
using TipsyBaboon.UI.Services;
using IAppAuthenticationService = TipsyBaboon.Core.Interfaces.IAuthenticationService;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account
{
    public class RegisterModel : IdentityPageModel
    {
        private readonly IAppAuthenticationService _authService;
        private readonly IConfigurationPageService? _configService;
        private readonly UserGovernanceService _governanceService;

        public RegisterModel(
            IAppAuthenticationService authService,
            TipsyBaboonIdentityPageOptions options,
            UserGovernanceService governanceService,
            IConfigurationPageService? configService = null) : base(options)
        {
            _authService = authService;
            _configService = configService;
            _governanceService = governanceService;
        }

        public bool AllowRegistration { get; set; } = true;

        [BindProperty]
        public InputModel Input { get; set; } = default!;

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = default!;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = default!;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = default!;
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            var uiConfig = await LoadUIConfigurationAsync();
            AllowRegistration = uiConfig.AllowRegistration;

            if (!AllowRegistration)
            {
                return RedirectToPage("./Login", new { returnUrl });
            }

            ReturnUrl = returnUrl;
            ExternalLogins = (await HttpContext.GetExternalProvidersAsync()).ToList();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            var uiConfig = await LoadUIConfigurationAsync();
            if (!uiConfig.AllowRegistration)
            {
                return RedirectToPage("./Login", new { returnUrl });
            }

            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await HttpContext.GetExternalProvidersAsync()).ToList();

            if (ModelState.IsValid)
            {
                var existingUser = await _authService.FindByEmailAsync(Input.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "A user with this email already exists.");
                    return Page();
                }

                var user = await _authService.CreateUserAsync(Input.Email, Input.Email, Input.Password);

                await _governanceService.AssignFirstUserAdminIfNeededAsync(user);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? "")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return LocalRedirect(returnUrl);
            }

            return Page();
        }

        private async Task<UIConfiguration> LoadUIConfigurationAsync()
        {
            if (_configService != null)
            {
                try
                {
                    return await _configService.LoadConfigurationAsync<UIConfiguration>();
                }
                catch
                {
                }
            }
            return UIConfiguration.Instance;
        }
    }
}
