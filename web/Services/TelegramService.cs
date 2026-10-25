using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Services
{
    // Thin wrapper around the Telegram Bot API, plus the weekly summary text builder
    // shared by the scheduled job (TelegramWeeklySummaryService) and the on-demand
    // /summary command (TelegramController).
    public class TelegramService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public TelegramService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        private string? BotToken => _config["Telegram:BotToken"];

        public async Task SendMessageAsync(long chatId, string text, CancellationToken ct = default)
        {
            var token = BotToken;
            if (string.IsNullOrWhiteSpace(token)) return;

            var payload = new { chat_id = chatId, text };
            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            await _http.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", content, ct);
        }

        // Builds the "last 7 days" listening summary text for one user.
        public async Task<string> BuildWeeklySummaryAsync(string userId, CancellationToken ct = default)
        {
            var connStr = SqlConnectionFactory.DefaultConnection(_config);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            const string sevenDayFilter =
                "user_id = @uid AND played_at >= DATEADD(day, -7, SYSUTCDATETIME())";

            var totalPlays = await ScalarAsync(conn,
                $"SELECT COUNT(*) FROM dbo.music_history WHERE {sevenDayFilter}", userId);

            var sb = new StringBuilder();
            sb.AppendLine("🎧 Your Statify weekly summary");
            sb.AppendLine();

            if (totalPlays == 0)
            {
                sb.AppendLine("No plays logged this week — go listen to something!");
                return sb.ToString();
            }

            var uniqueArtists = await ScalarAsync(conn,
                $"SELECT COUNT(DISTINCT artist) FROM dbo.music_history WHERE {sevenDayFilter} AND artist IS NOT NULL", userId);

            var topArtists = await TopAsync(conn, $@"
                SELECT TOP 3 artist, COUNT(*) AS cnt
                FROM dbo.music_history
                WHERE {sevenDayFilter} AND artist IS NOT NULL
                GROUP BY artist ORDER BY cnt DESC", userId);

            var topTracks = await TopAsync(conn, $@"
                SELECT TOP 3 song + ' — ' + ISNULL(artist, 'unknown'), COUNT(*) AS cnt
                FROM dbo.music_history
                WHERE {sevenDayFilter} AND song IS NOT NULL
                GROUP BY song, artist ORDER BY cnt DESC", userId);

            sb.AppendLine($"Plays: {totalPlays}");
            sb.AppendLine($"Unique artists: {uniqueArtists}");
            sb.AppendLine();

            sb.AppendLine("Top artists:");
            foreach (var (name, count) in topArtists)
                sb.AppendLine($"  • {name} ({count})");

            sb.AppendLine();
            sb.AppendLine("Top tracks:");
            foreach (var (name, count) in topTracks)
                sb.AppendLine($"  • {name} ({count})");

            return sb.ToString();
        }

        private static async Task<int> ScalarAsync(SqlConnection conn, string sql, string userId)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        private static async Task<List<(string Name, int Count)>> TopAsync(SqlConnection conn, string sql, string userId)
        {
            var list = new List<(string, int)>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add((reader.GetString(0), reader.GetInt32(1)));
            return list;
        }
    }
}
