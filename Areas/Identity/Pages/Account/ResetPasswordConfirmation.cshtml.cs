// Licensed to the .NET Foundation under one or more agreements.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SpotifyStatisticsWebApp.Areas.Identity.Pages.Account
{
    // Доступна без логина — показывается после успешного сброса пароля
    [AllowAnonymous]
    public class ResetPasswordConfirmationModel : PageModel
    {
        public void OnGet() { }
    }
}
