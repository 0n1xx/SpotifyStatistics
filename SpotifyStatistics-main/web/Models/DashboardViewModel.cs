namespace SpotifyStatisticsWebApp.Models
{
    public class DashboardViewModel
    {
        public int TotalTracks { get; set; }
        public int UniqueArtists { get; set; }
        public int UniqueAlbums { get; set; }
        public int UniqueCountries { get; set; }
        public List<TopItem> TopTracks { get; set; } = new();
        public List<TopItem> TopArtists { get; set; } = new();
        public List<TopItem> TopAlbums { get; set; } = new();
        public List<CountryCount> CountryCounts { get; set; } = new();
        public List<int> ListeningByHour { get; set; } = new(new int[24]);
        public List<MonthCount> ActivityByMonth { get; set; } = new();
    }

    public class TopItem
    {
        public string? Name { get; set; }
        public string? Sub { get; set; }
        public int Count { get; set; }
    }

    public class CountryCount
    {
        public string? Country { get; set; }
        public int Count { get; set; }
    }

    public class MonthCount
    {
        public string? Month { get; set; }
        public int Count { get; set; }
    }
}