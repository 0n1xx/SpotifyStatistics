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
        public bool SpotifyConnected { get; set; }
        public bool GoogleConnected { get; set; }
        public bool GitHubConnected { get; set; }

        public SettingsModel(IConfiguration config)
        {
            _config = config;
        }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var connStr = _config.GetConnectionString("DefaultConnection");

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

        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            // TODO: implement full account deletion
            return RedirectToPage("/Index");
        }
    }
}