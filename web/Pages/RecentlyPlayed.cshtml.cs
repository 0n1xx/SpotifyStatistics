using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Pages
{
    // The page now loads no tracks server-side.
    // All data is fetched client-side via GET /api/history?page=&limit=
    // using the same JWT-authenticated endpoint the iOS app uses.
    // The page model only sets up sidebar state (avatar, display name, Spotify status)
    // and passes the total count for the header.
    [Authorize]
    public class RecentlyPlayedModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        public bool SpotifyConnected { get; set; }
        public int TotalCount { get; set; }

        public RecentlyPlayedModel(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db = db;
        }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Avatar / display name for sidebar
            try
            {
                var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                ViewData["AvatarDataUrl"] = profile?.AvatarBase64;
                ViewData["DisplayName"]   = profile?.DisplayName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profile fetch error: {ex.Message}");
            }

            // Spotify connection status for sidebar badge
            try
            {
                var defaultConn = _config.GetConnectionString("DefaultConnection");
                using var spotifyDb = new SqlConnection(defaultConn);
                await spotifyDb.OpenAsync();
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM SpotifyTokens WHERE UserId = @uid", spotifyDb);
                checkCmd.Parameters.AddWithValue("@uid", userId);
                SpotifyConnected = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SpotifyTokens check error: {ex.Message}");
            }

            // Total count for the page header — lightweight COUNT(*) only
            var connStr = _config.GetConnectionString("MusicHistoryConnection");
            if (string.IsNullOrEmpty(connStr)) return;

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                using var countCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.music_history WHERE user_id = @uid", conn);
                countCmd.Parameters.AddWithValue("@uid", userId);
                TotalCount = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RecentlyPlayed count error: {ex.Message}");
            }
        }
    }
}
