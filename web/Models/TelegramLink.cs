namespace SpotifyStatisticsWebApp.Models
{
    // One row per Statify user. ChatId is set once the user finishes the
    // /start <token> handshake in Telegram; LinkToken is only used during that handshake.
    public class TelegramLink
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public long? ChatId { get; set; }
        public string? LinkToken { get; set; }
        public DateTime? LinkTokenExpiresAtUtc { get; set; }
        public bool WeeklySummaryEnabled { get; set; } = true;
        public DateTime? LastSummarySentAtUtc { get; set; }
    }
}
