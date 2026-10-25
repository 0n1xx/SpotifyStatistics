using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Data;
using System.Text;

namespace SpotifyStatisticsWebApp.Services
{
    // Sends a weekly Telegram listening summary to every linked, opted-in user.
    // Checks hourly and fires once per user per week (gated by LastSummarySentAtUtc)
    // so a missed exact hour (deploy, restart) still gets caught on the next check.
    public class TelegramWeeklySummaryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TelegramWeeklySummaryService> _logger;

        // Monday 09:00 UTC — adjust to taste.
        private const DayOfWeek SendDay = DayOfWeek.Monday;
        private const int SendHourUtc = 9;

        public TelegramWeeklySummaryService(
            IServiceScopeFactory scopeFactory,
            ILogger<TelegramWeeklySummaryService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the startup migration block in Program.cs finish first.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    if (now.DayOfWeek == SendDay && now.Hour == SendHourUtc)
                        await SendDueSummariesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Weekly Telegram summary run failed");
                }

                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
                catch (TaskCanceledException) { }
            }
        }

        private async Task SendDueSummariesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // Guards against double-sending if the app restarts mid-window.
            var cutoff = DateTime.UtcNow.AddDays(-6);
            var dueLinks = await db.TelegramLinks
                .Where(l => l.ChatId != null && l.WeeklySummaryEnabled
                    && (l.LastSummarySentAtUtc == null || l.LastSummarySentAtUtc < cutoff))
                .ToListAsync(ct);

            if (dueLinks.Count == 0) return;

            var connStr = SqlConnectionFactory.DefaultConnection(config);

            foreach (var link in dueLinks)
            {
                try
                {
                    var summary = await BuildWeeklySummaryAsync(connStr, link.UserId, ct);
                    await telegram.SendMessageAsync(link.ChatId!.Value, summary, ct);

                    link.LastSummarySentAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send weekly Telegram summary to user {UserId}", link.UserId);
                }
            }
        }

        private static async Task<string> BuildWeeklySummaryAsync(string connStr, string userId, CancellationToken ct)
        {
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
