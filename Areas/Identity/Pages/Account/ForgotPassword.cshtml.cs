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

            // Всегда редиректим на confirmation — не раскрываем существует ли аккаунт.
            // Письмо отправляем только если пользователь найден.
            if (user != null)
            {
                // Генерируем токен сброса пароля через Identity
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // Кодируем токен в Base64Url — безопасно для передачи в URL
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                // Строим ссылку на страницу ResetPassword с токеном и email
                var resetLink = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", token = encodedToken, email = Input.Email },
                    protocol: Request.Scheme);

                // HTML письмо в стиле Statify
                var htmlBody = $@"
                    <div style=""font-family:'DM Sans',sans-serif;background:#080808;color:#f0f0f0;padding:40px 0;"">
                      <div style=""max-width:480px;margin:0 auto;background:#111;border-radius:16px;padding:40px;border:1px solid rgba(255,255,255,0.07);"">
                        <div style=""display:flex;align-items:center;gap:10px;margin-bottom:32px;"">
                          <div style=""width:36px;height:36px;background:#1DB954;border-radius:50%;display:flex;align-items:center;justify-content:center;"">
                            <span style=""color:#000;font-weight:800;font-size:16px;"">S</span>
                          </div>
                          <span style=""font-size:20px;font-weight:800;letter-spacing:-0.5px;"">Statify</span>
                        </div>
                        <h1 style=""font-size:22px;font-weight:700;margin-bottom:12px;"">Reset your password</h1>
                        <p style=""font-size:14px;color:#888;line-height:1.6;margin-bottom:28px;"">
                          We received a request to reset the password for your Statify account.
                          Click the button below to choose a new password.
                        </p>
                        <a href=""{HtmlEncoder.Default.Encode(resetLink)}""
                           style=""display:inline-block;background:#1DB954;color:#000;font-weight:600;font-size:15px;padding:14px 28px;border-radius:10px;text-decoration:none;"">
                          Reset password
                        </a>
                        <p style=""font-size:12px;color:#555;margin-top:28px;line-height:1.6;"">
                          If you didn't request a password reset, you can safely ignore this email.
                          This link expires in 24 hours.
                        </p>
                      </div>
                    </div>";

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Reset your Statify password",
                    htmlBody);
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
