namespace SpotifyStatisticsWebApp.Models
{
    public class GoogleCalendarToken
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}

