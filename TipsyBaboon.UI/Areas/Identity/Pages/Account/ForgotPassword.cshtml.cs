using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.UI.Configuration;

namespace TipsyBaboon.UI.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : IdentityPageModel
    {
        private readonly IAuthenticationService _authService;

        public ForgotPasswordModel(IAuthenticationService authService, TipsyBaboonIdentityPageOptions options) : base(options)
        {
            _authService = authService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = default!;

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = default!;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _authService.FindByEmailAsync(Input.Email);

                // Always redirect to confirmation to prevent user enumeration
                // In production, this would send an email with a reset link
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
