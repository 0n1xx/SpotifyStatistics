using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Pages
{
    [Authorize]
    public class SettingsModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        public bool SpotifyConnected { get; set; }
        public bool GoogleConnected { get; set; }
        public bool GitHubConnected { get; set; }
        public string? AvatarUrl { get; set; }

        public SettingsModel(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var connStr = _config.GetConnectionString("DefaultConnection");

            // Check for existing avatar file
            if (userId != null)
            {
                var avatarsDir = Path.Combine(_env.WebRootPath, "avatars");
                foreach (var ext in new[] { "jpg", "jpeg", "png", "gif", "webp" })
                {
                    var candidate = $"{userId}.{ext}";
                    if (System.IO.File.Exists(Path.Combine(avatarsDir, candidate)))
                    {
                        AvatarUrl = $"/avatars/{candidate}";
                        break;
                    }
                }
            }

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM SpotifyTokens WHERE UserId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            SpotifyConnected = (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;

            // Check external logins
            using var cmd2 = new SqlCommand(
                "SELECT LoginProvider FROM AspNetUserLogins WHERE UserId = @uid", conn);
            cmd2.Parameters.AddWithValue("@uid", userId);
            using var reader = await cmd2.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = reader.GetString(0);
                if (provider == "Google") GoogleConnected = true;
                if (provider == "GitHub") GitHubConnected = true;
            }
        }

        public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
                return new JsonResult(new { success = false, error = "No file provided" });

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(avatar.ContentType.ToLower()))
                return new JsonResult(new { success = false, error = "Invalid file type. Please upload a JPEG, PNG, GIF, or WebP image." });

            // Max 5 MB
            if (avatar.Length > 5 * 1024 * 1024)
                return new JsonResult(new { success = false, error = "File too large. Maximum size is 5 MB." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ext = Path.GetExtension(avatar.FileName).TrimStart('.').ToLower();
            if (ext == "jpg") ext = "jpeg";

            var avatarsDir = Path.Combine(_env.WebRootPath, "avatars");
            Directory.CreateDirectory(avatarsDir);

            // Delete old avatars for this user
            foreach (var oldExt in new[] { "jpg", "jpeg", "png", "gif", "webp" })
            {
                var old = Path.Combine(avatarsDir, $"{userId}.{oldExt}");
                if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            }

            var filePath = Path.Combine(avatarsDir, $"{userId}.{ext}");
            using (var stream = System.IO.File.Create(filePath))
                await avatar.CopyToAsync(stream);

            var url = $"/avatars/{userId}.{ext}";
            return new JsonResult(new { success = true, url });
        }

        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            // TODO: implement full account deletion
            return RedirectToPage("/Index");
        }
    }
}