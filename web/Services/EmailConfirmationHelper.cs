using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace SpotifyStatisticsWebApp.Services
{
    /// <summary>
    /// Shared helper for sending email-confirmation links (register + login resend).
    /// </summary>
    public static class EmailConfirmationHelper
    {
        public const string RequiredMessage =
            "Sorry — we added email confirmation for account security. " +
            "We just sent a link to your inbox. Open it, then sign in again.";

        public static async Task SendAsync(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            IUrlHelper url,
            IdentityUser user,
            string scheme)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
                return;

            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code },
                protocol: scheme)!;

            await emailSender.SendEmailAsync(
                user.Email,
                "Confirm your email",
                ResendEmailSender.BuildConfirmEmailHtml(callbackUrl));
        }
    }
}
