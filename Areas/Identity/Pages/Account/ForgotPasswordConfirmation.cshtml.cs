// Licensed to the .NET Foundation under one or more agreements.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SpotifyStatisticsWebApp.Areas.Identity.Pages.Account
{
    // Accessible without login — shown after submitting forgot password form
    [AllowAnonymous]
    public class ForgotPasswordConfirmationModel : PageModel
    {
        public void OnGet() { }
    }
}
