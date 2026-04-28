// Licensed to the .NET Foundation under one or more agreements.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SpotifyStatisticsWebApp.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ForgotPasswordModel(UserManager<IdentityUser> userManager)
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
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            // TODO: когда подключим email sender (Resend/SendGrid),
            // здесь будет генерация токена и отправка письма.
            // Пока просто редиректим на confirmation — не раскрываем
            // существует ли аккаунт с таким email (security best practice).
            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
