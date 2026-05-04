// Licensed to the .NET Foundation under one or more agreements.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace SpotifyStatisticsWebApp.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ResetPasswordModel(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 6,
                ErrorMessage = "Password must be at least {2} characters long.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; }

            // Token from the reset email link — decoded from Base64Url in OnGet
            public string Token { get; set; }
        }

        public IActionResult OnGet(string token, string email)
        {
            // Invalid link — token is required
            if (token == null) return RedirectToPage("/Index");

            // Decode the token from Base64Url back to a plain string
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

            Input = new InputModel
            {
                Token = decodedToken,
                Email = email
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // User not found — redirect as if successful (security best practice)
            if (user == null) return RedirectToPage("./ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(
                user, Input.Token, Input.Password);

            if (result.Succeeded)
                return RedirectToPage("./ResetPasswordConfirmation");

            // Show errors — e.g. expired token or password too weak
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }
    }
}
