namespace SpotifyStatisticsWebApp.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string? AvatarBase64 { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
