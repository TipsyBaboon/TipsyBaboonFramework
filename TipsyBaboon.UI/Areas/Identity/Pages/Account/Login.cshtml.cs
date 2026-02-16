using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TipsyBaboon.UI.Configuration;
using IAppAuthenticationService = TipsyBaboon.Core.Interfaces.IAuthenticationService;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account
{
    public class LoginModel : IdentityPageModel
    {
        private readonly IAppAuthenticationService _authService;

        public LoginModel(IAppAuthenticationService authService, TipsyBaboonIdentityPageOptions options) : base(options)
        {
            _authService = authService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = default!;

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = default!;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = default!;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            ExternalLogins = (await HttpContext.GetExternalProvidersAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await HttpContext.GetExternalProvidersAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = await _authService.AuthenticateAsync(Input.Email, Input.Password);

                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? ""),
                        new Claim(ClaimTypes.Email, user.Email ?? "")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = Input.RememberMe,
                        ExpiresUtc = Input.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(2)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    return LocalRedirect(returnUrl);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            return Page();
        }
    }

    public static class AuthenticationHttpContextExtensions
    {
        public static async Task<IEnumerable<AuthenticationScheme>> GetExternalProvidersAsync(this HttpContext context)
        {
            var schemes = await context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
            return schemes.Where(x => !string.IsNullOrEmpty(x.DisplayName));
        }
    }
}
