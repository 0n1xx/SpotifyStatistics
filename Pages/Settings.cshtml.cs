using Microsoft.AspNetCore.Authorization;
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

        public bool SpotifyConnected { get; set; }
        public bool GoogleConnected { get; set; }
        public bool GitHubConnected { get; set; }
        public string? AvatarDataUrl { get; set; }
        public string? PhoneNumber { get; set; }

        public SettingsModel(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db = db;
        }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var connStr = _config.GetConnectionString("DefaultConnection");

            // Load profile from DB (avatar + phone persist across deploys)
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            AvatarDataUrl = profile?.AvatarBase64;
            PhoneNumber   = profile?.PhoneNumber;
            ViewData["AvatarDataUrl"] = AvatarDataUrl;

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

        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            // TODO: implement full account deletion
            return RedirectToPage("/Index");
        }
    }
}
