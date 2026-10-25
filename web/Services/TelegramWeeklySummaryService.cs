using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Data;

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

            // Guards against double-sending if the app restarts mid-window.
            var cutoff = DateTime.UtcNow.AddDays(-6);
            var dueLinks = await db.TelegramLinks
                .Where(l => l.ChatId != null && l.WeeklySummaryEnabled
                    && (l.LastSummarySentAtUtc == null || l.LastSummarySentAtUtc < cutoff))
                .ToListAsync(ct);

            if (dueLinks.Count == 0) return;

            foreach (var link in dueLinks)
            {
                try
                {
                    var summary = await telegram.BuildWeeklySummaryAsync(link.UserId, ct);
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
    }
}
