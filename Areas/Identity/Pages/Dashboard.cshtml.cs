using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using SpotifyStatisticsWebApp.Models;

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

            Data.TotalTracks = await ScalarAsync<int>(conn,
                "SELECT COUNT(*) FROM music_history");

            Data.UniqueArtists = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT artist) FROM music_history");

            Data.UniqueAlbums = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT album) FROM music_history");

            Data.UniqueCountries = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT country) FROM music_history WHERE country != 'unknown'");

            Data.TopTracks = await QueryListAsync(conn,
                @"SELECT TOP 10 song as Name, artist as Sub, COUNT(*) as Count 
                  FROM music_history WHERE song IS NOT NULL 
                  GROUP BY song, artist ORDER BY Count DESC");

            Data.TopArtists = await QueryListAsync(conn,
                @"SELECT TOP 10 artist as Name, country as Sub, COUNT(*) as Count 
                  FROM music_history WHERE artist IS NOT NULL 
                  GROUP BY artist, country ORDER BY Count DESC");

            Data.TopAlbums = await QueryListAsync(conn,
                @"SELECT TOP 10 album as Name, artist as Sub, COUNT(*) as Count 
                  FROM music_history WHERE album IS NOT NULL 
                  GROUP BY album, artist ORDER BY Count DESC");

            using (var cmd = new SqlCommand(
                @"SELECT country, COUNT(*) as cnt FROM music_history 
                  WHERE country != 'unknown'
                  GROUP BY country ORDER BY cnt DESC", conn))
            {
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
                  FROM music_history
                  GROUP BY DATEPART(HOUR, played_at)", conn))
            {
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
                  FROM music_history
                  GROUP BY FORMAT(played_at, 'MMM yyyy'), YEAR(played_at), MONTH(played_at)
                  ORDER BY YEAR(played_at), MONTH(played_at)", conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    Data.ActivityByMonth.Add(new MonthCount
                    {
                        Month = reader.GetString(0),
                        Count = reader.GetInt32(1)
                    });
            }
        }

        private async Task<T> ScalarAsync<T>(SqlConnection conn, string sql)
        {
            using var cmd = new SqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result ?? 0, typeof(T));
        }

        private async Task<List<TopItem>> QueryListAsync(SqlConnection conn, string sql)
        {
            var list = new List<TopItem>();
            using var cmd = new SqlCommand(sql, conn);
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