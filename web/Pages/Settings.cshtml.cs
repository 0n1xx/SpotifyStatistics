using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Models;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Pages
{
    [Authorize]
    public class SettingsModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public bool SpotifyConnected { get; set; }
        public bool GoogleConnected { get; set; }
        public bool GitHubConnected { get; set; }
        public string? AvatarDataUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string? DisplayName { get; set; }

        public SettingsModel(IConfiguration config, ApplicationDbContext db,
            UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _config = config;
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var connStr = _config.GetConnectionString("DefaultConnection");

            // Load profile from DB (avatar + phone persist across deploys)
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            AvatarDataUrl = profile?.AvatarBase64;
            PhoneNumber   = profile?.PhoneNumber;
            DisplayName   = profile?.DisplayName;
            ViewData["AvatarDataUrl"] = AvatarDataUrl;
            ViewData["DisplayName"]   = DisplayName;

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM SpotifyTokens WHERE UserId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            SpotifyConnected = (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;

            using var cmd2 = new SqlCommand(
                "SELECT LoginProvider FROM AspNetUserLogins WHERE UserId = @uid", conn);
            cmd2.Parameters.AddWithValue("@uid", userId);
            using var reader = await cmd2.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = reader.GetString(0);
                if (provider == "Google") GoogleConnected = true;
                if (provider == "GitHub")  GitHubConnected  = true;
            }
        }

        public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
                return new JsonResult(new { success = false, error = "No file provided" });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(avatar.ContentType.ToLower()))
                return new JsonResult(new { success = false, error = "Invalid file type." });

            if (avatar.Length > 5 * 1024 * 1024)
                return new JsonResult(new { success = false, error = "File too large. Max 5 MB." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            using var ms = new MemoryStream();
            await avatar.CopyToAsync(ms);
            var dataUrl = $"data:{avatar.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";

            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
                _db.UserProfiles.Add(new UserProfile { UserId = userId, AvatarBase64 = dataUrl });
            else
                profile.AvatarBase64 = dataUrl;

            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true, url = dataUrl });
        }

        public async Task<IActionResult> OnPostSavePhoneAsync(string phone)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
                _db.UserProfiles.Add(new UserProfile { UserId = userId, PhoneNumber = phone });
            else
                profile.PhoneNumber = phone;

            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        // ── SaveUsername handler ──────────────────────────────────────────────────
        // Saves the user-chosen display name to UserProfiles.
        // Max 50 chars — validated here and in JS before the request is made.
        public async Task<IActionResult> OnPostSaveUsernameAsync(string username)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Trim and enforce max length server-side
            username = (username ?? "").Trim();
            if (username.Length > 50)
                return new JsonResult(new { success = false, error = "Max 50 characters" });

            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
                _db.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = username });
            else
                profile.DisplayName = username;

            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        // ── Link external OAuth provider to existing account ─────────────────────
        // Called when user clicks Connect for Google/GitHub on Settings page.
        // Uses the same ASP.NET Identity LinkLogin flow as /Manage/ExternalLogins
        // but returns to /Settings instead of /Manage/ExternalLogins.
        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            var userId = _userManager.GetUserId(User);
            var redirectUrl = Url.Page("./Settings", pageHandler: "LinkLoginCallback");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, userId);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var userId = await _userManager.GetUserIdAsync(user);
            var info   = await _signInManager.GetExternalLoginInfoAsync(userId);
            if (info == null)
            {
                TempData["StatusMessage"] = "Error: Could not load external login info.";
                return RedirectToPage();
            }

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                TempData["StatusMessage"] = "Error: That account is already linked to another user.";
                return RedirectToPage();
            }

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            TempData["StatusMessage"] = $"{info.LoginProvider} account connected successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Index");

            var userId = user.Id;

            // Delete UserProfile (avatar + phone)
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null) _db.UserProfiles.Remove(profile);

            // Delete SpotifyTokens
            var connStr = _config.GetConnectionString("DefaultConnection");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM SpotifyTokens WHERE UserId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            await cmd.ExecuteNonQueryAsync();

            await _db.SaveChangesAsync();

            // Delete the Identity user (cascades to AspNetUserLogins, Claims, etc.)
            await _signInManager.SignOutAsync();
            await _userManager.DeleteAsync(user);

            return RedirectToPage("/Index");
        }
    }
}
