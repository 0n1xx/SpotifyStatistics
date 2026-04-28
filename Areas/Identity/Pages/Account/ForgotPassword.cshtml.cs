// Licensed to the .NET Foundation under one or more agreements.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using SpotifyStatisticsWebApp.Services;

namespace SpotifyStatisticsWebApp.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Always redirect to confirmation — never reveal whether the account exists.
            // Only send the email if the user was actually found.
            if (user != null)
            {
                // Generate a password reset token via ASP.NET Identity
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // Encode the token as Base64Url so it's safe to include in a URL
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                // Build the reset link pointing to the ResetPassword page
                var resetLink = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", token = encodedToken, email = Input.Email },
                    protocol: Request.Scheme);

                // Use the branded HTML builder from ResendEmailSender
                var htmlBody = ResendEmailSender.BuildResetEmailHtml(
                    HtmlEncoder.Default.Encode(resetLink));

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Reset your Statify password",
                    htmlBody);
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
