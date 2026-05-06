using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Pages
{
    [Authorize]
    public class RecentlyPlayedModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        public List<RecentTrack> Tracks { get; set; } = new();
        public bool SpotifyConnected { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalPages { get; set; }

        public RecentlyPlayedModel(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db = db;
        }

        public async Task OnGetAsync(int page = 1)
        {
            Page = Math.Max(1, page);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Avatar from DB for sidebar
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

            // Check Spotify connection
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

            var connStr = _config.GetConnectionString("MusicHistoryConnection");
            if (string.IsNullOrEmpty(connStr)) return; // No music DB configured yet

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Total count for pagination
                using var countCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.music_history WHERE user_id = @uid", conn);
                countCmd.Parameters.AddWithValue("@uid", userId);
                TotalCount = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
                TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);

                int offset = (Page - 1) * PageSize;
                using var cmd = new SqlCommand(@"
                    SELECT song, artist, album, country, played_at
                    FROM dbo.music_history
                    WHERE user_id = @uid
                    ORDER BY played_at DESC
                    OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY", conn);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@size", PageSize);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Tracks.Add(new RecentTrack
                    {
                        Song    = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        Artist  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Album   = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Country = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PlayedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4)
                    });
                }
            }
            catch (Exception ex)
            {
                // DB unavailable or query error — show empty state instead of 500
                Console.WriteLine($"RecentlyPlayed error: {ex.Message}");
            }
        }
    }

    public class RecentTrack
    {
        public string Song { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime PlayedAt { get; set; }
    }
}
