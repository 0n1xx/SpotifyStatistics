namespace SpotifyStatisticsWebApp.Models
{
    public class MusicHistory
    {
        public DateTime PlayedAt { get; set; }
        public string? Song { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public DateTime? Date { get; set; }
        public string? Country { get; set; }
        public string? BeginArea { get; set; }
        public string? UserId { get; set; }
    }
}