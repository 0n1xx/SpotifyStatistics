using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using SpotifyStatisticsWebApp.Models;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        public DashboardViewModel Data { get; set; } = new();
        public bool SpotifyConnected { get; set; }

        public DashboardModel(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db = db;
        }

        [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
        public string Range { get; set; } = "all";

        private string DateFilter() => Range switch
        {
            "7d"  => "AND played_at >= DATEADD(day,   -7,  GETUTCDATE())",
            "30d" => "AND played_at >= DATEADD(day,   -30, GETUTCDATE())",
            "6m"  => "AND played_at >= DATEADD(month, -6,  GETUTCDATE())",
            _     => ""
        };

        public async Task OnGetAsync()
        {
            if (Range is not ("7d" or "30d" or "6m" or "all")) Range = "all";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Avatar from DB for sidebar
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            ViewData["AvatarDataUrl"] = profile?.AvatarBase64;
            ViewData["DisplayName"]   = profile?.DisplayName;

            // Check Spotify connection
            var spotifyConn = _config.GetConnectionString("DefaultConnection");
            using var spotifyDb = new SqlConnection(spotifyConn);
            await spotifyDb.OpenAsync();
            using var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM SpotifyTokens WHERE UserId = @uid", spotifyDb);
            checkCmd.Parameters.AddWithValue("@uid", userId);
            SpotifyConnected = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

            // Music history queries
            var connStr = _config.GetConnectionString("MusicHistoryConnection");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var df = DateFilter();

            Data.TotalTracks = await ScalarAsync<int>(conn,
                $"SELECT COUNT(*) FROM music_history WHERE user_id = @uid {df}", userId);

            Data.UniqueArtists = await ScalarAsync<int>(conn,
                $"SELECT COUNT(DISTINCT artist) FROM music_history WHERE user_id = @uid {df}", userId);

            Data.UniqueAlbums = await ScalarAsync<int>(conn,
                $"SELECT COUNT(DISTINCT album) FROM music_history WHERE user_id = @uid {df}", userId);

            Data.UniqueCountries = await ScalarAsync<int>(conn,
                $"SELECT COUNT(DISTINCT country) FROM music_history WHERE user_id = @uid AND country != 'unknown' {df}", userId);

            Data.TopTracks = await QueryListAsync(conn,
                $@"SELECT TOP 10 song as Name, artist as Sub, COUNT(*) as Count
                  FROM music_history WHERE user_id = @uid AND song IS NOT NULL {df}
                  GROUP BY song, artist ORDER BY Count DESC", userId);

            Data.TopArtists = await QueryListAsync(conn,
                $@"SELECT TOP 10 artist as Name, country as Sub, COUNT(*) as Count
                  FROM music_history WHERE user_id = @uid AND artist IS NOT NULL {df}
                  GROUP BY artist, country ORDER BY Count DESC", userId);

            Data.TopAlbums = await QueryListAsync(conn,
                $@"SELECT TOP 10 album as Name, artist as Sub, COUNT(*) as Count
                  FROM music_history WHERE user_id = @uid AND album IS NOT NULL {df}
                  GROUP BY album, artist ORDER BY Count DESC", userId);

            using (var cmd = new SqlCommand(
                $@"SELECT country, COUNT(*) as cnt FROM music_history
                  WHERE user_id = @uid AND country != 'unknown' {df}
                  GROUP BY country ORDER BY cnt DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    Data.CountryCounts.Add(new CountryCount
                    {
                        Country = reader.GetString(0),
                        Count = reader.GetInt32(1)
                    });
            }

            using (var cmd = new SqlCommand(
                $@"SELECT DATEPART(HOUR, played_at) as hr, COUNT(*) as cnt
                  FROM music_history WHERE user_id = @uid {df}
                  GROUP BY DATEPART(HOUR, played_at)", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int hr = reader.GetInt32(0);
                    if (hr >= 0 && hr < 24)
                        Data.ListeningByHour[hr] = reader.GetInt32(1);
                }
            }

            using (var cmd = new SqlCommand(
                $@"SELECT FORMAT(played_at, 'MMM yyyy') as mo, COUNT(*) as cnt
                  FROM music_history WHERE user_id = @uid {df}
                  GROUP BY FORMAT(played_at, 'MMM yyyy'), YEAR(played_at), MONTH(played_at)
                  ORDER BY YEAR(played_at), MONTH(played_at)", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    Data.ActivityByMonth.Add(new MonthCount
                    {
                        Month = reader.GetString(0),
                        Count = reader.GetInt32(1)
                    });
            }
        }

        private async Task<T> ScalarAsync<T>(SqlConnection conn, string sql, string? userId = null)
        {
            using var cmd = new SqlCommand(sql, conn);
            if (userId != null) cmd.Parameters.AddWithValue("@uid", userId);
            var result = await cmd.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result ?? 0, typeof(T));
        }

        private async Task<List<TopItem>> QueryListAsync(SqlConnection conn, string sql, string? userId = null)
        {
            var list = new List<TopItem>();
            using var cmd = new SqlCommand(sql, conn);
            if (userId != null) cmd.Parameters.AddWithValue("@uid", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new TopItem
                {
                    Name = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Sub = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Count = reader.GetInt32(2)
                });
            return list;
        }
    }
}