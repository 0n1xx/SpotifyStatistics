using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SpotifyStatisticsWebApp.Controllers
{
    /// <summary>
    /// Handles linking external OAuth providers (Google, GitHub) to an existing
    /// authenticated account — without going through the full login/register flow.
    /// Invoked from Settings page via simple GET links so no CSRF form is needed.
    /// </summary>
    [Authorize]
    [Route("account")]
    public class AccountLinkController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public AccountLinkController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager   = userManager;
        }

        /// <summary>
        /// GET /account/link-login?provider=Google|GitHub
        /// Clears any stale external cookie then challenges the requested provider.
        /// On success the browser is redirected to /account/link-login-callback.
        /// </summary>
        [HttpGet("link-login")]
        public async Task<IActionResult> LinkLogin(string provider)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            var userId      = _userManager.GetUserId(User);
            var callbackUrl = Url.Action(nameof(LinkLoginCallback), "AccountLink", null, Request.Scheme);
            var properties  = _signInManager.ConfigureExternalAuthenticationProperties(
                provider, callbackUrl, userId);

            return Challenge(properties, provider);
        }

        /// <summary>
        /// GET /account/link-login-callback
        /// Called by the OAuth provider after the user grants permission.
        /// Adds the login to the current user's account and redirects back to Settings.
        /// </summary>
        [HttpGet("link-login-callback")]
        public async Task<IActionResult> LinkLoginCallback()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var userId = await _userManager.GetUserIdAsync(user);
            var info   = await _signInManager.GetExternalLoginInfoAsync(userId);

            if (info == null)
                return Redirect("/Settings?error=oauth-failed");

            // Link the provider to the existing account
            var result = await _userManager.AddLoginAsync(user, info);

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            if (!result.Succeeded)
            {
                // Most likely: provider already linked to a different account
                return Redirect("/Settings?error=already-linked");
            }

            return Redirect("/Settings?linked=" + info.LoginProvider);
        }
    }
}
