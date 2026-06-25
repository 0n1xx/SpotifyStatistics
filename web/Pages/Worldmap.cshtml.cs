using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using SpotifyStatisticsWebApp.Models;
using SpotifyStatisticsWebApp.Services;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Pages
{
    [Authorize]
    public class WorldMapModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        public List<CountryCount> CountryCounts { get; set; } = new();
        public bool SpotifyConnected { get; set; }

        public WorldMapModel(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db = db;
        }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Avatar from DB for sidebar
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            ViewData["AvatarDataUrl"] = profile?.AvatarBase64;
            ViewData["DisplayName"]   = profile?.DisplayName;

            var connStr = SqlConnectionFactory.DefaultConnection(_config);
            using var spotifyDb = new SqlConnection(connStr);
            await spotifyDb.OpenAsync();
            using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM SpotifyTokens WHERE UserId = @uid", spotifyDb);
            checkCmd.Parameters.AddWithValue("@uid", userId);
            SpotifyConnected = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT country, COUNT(*) as cnt
                FROM music_history
                WHERE user_id = @uid AND country IS NOT NULL AND country != 'unknown'
                GROUP BY country
                ORDER BY cnt DESC", conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                CountryCounts.Add(new CountryCount
                {
                    Country = reader.GetString(0),
                    Count = reader.GetInt32(1)
                });
            }
        }
    }
}