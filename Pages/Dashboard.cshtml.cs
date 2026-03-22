using Microsoft.AspNetCore.Authorization;
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
        public DashboardViewModel Data { get; set; } = new();

        public DashboardModel(IConfiguration config)
        {
            _config = config;
        }

        public async Task OnGetAsync()
        {
            var connStr = _config.GetConnectionString("MusicHistoryConnection");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            Data.TotalTracks = await ScalarAsync<int>(conn,
                "SELECT COUNT(*) FROM music_history WHERE user_id = @uid", userId);

            Data.UniqueArtists = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT artist) FROM music_history WHERE user_id = @uid", userId);

            Data.UniqueAlbums = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT album) FROM music_history WHERE user_id = @uid", userId);

            Data.UniqueCountries = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT country) FROM music_history WHERE user_id = @uid AND country != 'unknown'", userId);

            Data.TopTracks = await QueryListAsync(conn,
                @"SELECT TOP 10 song as Name, artist as Sub, COUNT(*) as Count 
                  FROM music_history WHERE user_id = @uid AND song IS NOT NULL 
                  GROUP BY song, artist ORDER BY Count DESC", userId);

            Data.TopArtists = await QueryListAsync(conn,
                @"SELECT TOP 10 artist as Name, country as Sub, COUNT(*) as Count 
                  FROM music_history WHERE user_id = @uid AND artist IS NOT NULL 
                  GROUP BY artist, country ORDER BY Count DESC", userId);

            Data.TopAlbums = await QueryListAsync(conn,
                @"SELECT TOP 10 album as Name, artist as Sub, COUNT(*) as Count 
                  FROM music_history WHERE user_id = @uid AND album IS NOT NULL 
                  GROUP BY album, artist ORDER BY Count DESC", userId);

            using (var cmd = new SqlCommand(
                @"SELECT country, COUNT(*) as cnt FROM music_history 
                  WHERE user_id = @uid AND country != 'unknown'
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
                @"SELECT DATEPART(HOUR, played_at) as hr, COUNT(*) as cnt
                  FROM music_history WHERE user_id = @uid
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
                @"SELECT FORMAT(played_at, 'MMM yyyy') as mo, COUNT(*) as cnt
                  FROM music_history WHERE user_id = @uid
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