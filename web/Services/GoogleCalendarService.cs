using Microsoft.Data.SqlClient;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Services
{
    // Minimal Google Calendar read-only client.
    // Uses per-user OAuth tokens stored in dbo.GoogleCalendarTokens.
    public class GoogleCalendarService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public GoogleCalendarService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        // Reads events from ALL calendars visible to this Google account
        // (primary, Vlad & Temi, Tasks, AVT, etc.), not just one calendar.
        public async Task<string> GetUpcomingSummaryAsync(
            string userId,
            int maxEvents = 15,
            string? preferredSharedCalendarName = "Vlad & Temi")
        {
            var token = await GetOrRefreshTokenAsync(userId);
            if (token == null)
                return "Google Calendar is not connected for this account yet. Log out and sign in with Google again to grant calendar access.";

            // Use a daytime window that includes events already in progress.
            // timeMin = start of today (local Eastern), timeMax = +7 days.
            var eastern = GetEasternNow();
            var dayStartLocal = eastern.Date;
            var windowEndLocal = dayStartLocal.AddDays(7);

            var timeMin = Uri.EscapeDataString(ToUtcIso(dayStartLocal));
            var timeMax = Uri.EscapeDataString(ToUtcIso(windowEndLocal));

            var sources = await ListCalendarsAsync(token.AccessToken!);
            if (sources.Count == 0)
                return "Google Calendar: no calendars found on this account.";

            // Prefer putting shared calendar first in labels (optional aesthetic).
            sources = sources
                .OrderByDescending(c =>
                    preferredSharedCalendarName != null &&
                    string.Equals(c.Label, preferredSharedCalendarName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(c => c.Label)
                .ToList();

            var allEvents = new List<(DateTimeOffset SortKey, string Start, string End, string Summary, string Source)>();
            var errors = new List<string>();

            foreach (var (calendarId, label) in sources)
            {
                var url =
                    $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events" +
                    $"?timeMin={timeMin}&timeMax={timeMax}" +
                    "&singleEvents=true&orderBy=startTime" +
                    $"&maxResults={maxEvents}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                using var resp = await _http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    errors.Add($"{label}: HTTP {(int)resp.StatusCode}");
                    continue;
                }

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("items", out var items) ||
                    items.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var ev in items.EnumerateArray())
                {
                    var summary = ev.TryGetProperty("summary", out var s)
                        ? (s.GetString() ?? "(no title)")
                        : "(no title)";
                    var start = ReadEventDateTime(ev, "start");
                    var end = ReadEventDateTime(ev, "end");
                    var sortKey = ParseSortKey(start);
                    allEvents.Add((sortKey, start, end, summary, label));
                }
            }

            if (allEvents.Count == 0)
            {
                var err = errors.Count > 0 ? " Errors: " + string.Join("; ", errors) : "";
                return "Google Calendar: no upcoming events found." + err;
            }

            var todayLocal = eastern.Date;
            var todayEvents = allEvents
                .Where(e => IsSameLocalDay(e.SortKey, todayLocal))
                .OrderBy(e => e.SortKey)
                .ToList();

            var upcoming = allEvents
                .OrderBy(e => e.SortKey)
                .Take(maxEvents)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Google Calendar connected. Local date now: {eastern:yyyy-MM-dd HH:mm} (Eastern).");
            sb.AppendLine($"Calendars read: {string.Join(", ", sources.Select(x => x.Label))}");

            sb.AppendLine("Today's events/tasks:");
            if (todayEvents.Count == 0)
            {
                sb.AppendLine("  (none found for today)");
            }
            else
            {
                foreach (var ev in todayEvents)
                    sb.AppendLine($"  - [{ev.Source}] {ev.Summary} — {ev.Start} to {ev.End}");
            }

            sb.AppendLine("Upcoming (next days):");
            foreach (var ev in upcoming)
                sb.AppendLine($"  - [{ev.Source}] {ev.Summary} — {ev.Start} to {ev.End}");

            if (errors.Count > 0)
                sb.AppendLine("Partial calendar errors: " + string.Join("; ", errors));

            return sb.ToString();
        }

        private async Task<List<(string CalendarId, string Label)>> ListCalendarsAsync(string accessToken)
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                "https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=50");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return new List<(string, string)>();

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return new List<(string, string)>();

            var list = new List<(string, string)>();
            foreach (var item in items.EnumerateArray())
            {
                // Skip calendars user has hidden in Google Calendar UI if marked selected=false
                if (item.TryGetProperty("selected", out var selected) &&
                    selected.ValueKind == JsonValueKind.False)
                    continue;

                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var summary = item.TryGetProperty("summary", out var s) ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                list.Add((id, string.IsNullOrWhiteSpace(summary) ? id : summary!));
            }

            return list;
        }

        private static DateTime GetEasternNow()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(-4);
                }
            }
        }

        private static string ToUtcIso(DateTime localEasternUnspecified)
        {
            try
            {
                TimeZoneInfo tz;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"); }
                catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }

                var utc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localEasternUnspecified, DateTimeKind.Unspecified), tz);
                return utc.ToString("o");
            }
            catch
            {
                return DateTime.SpecifyKind(localEasternUnspecified, DateTimeKind.Utc).ToString("o");
            }
        }

        private static DateTimeOffset ParseSortKey(string start)
        {
            if (DateTimeOffset.TryParse(start, out var dto)) return dto;
            if (DateTime.TryParse(start, out var d))
                return new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc));
            return DateTimeOffset.MaxValue;
        }

        private static bool IsSameLocalDay(DateTimeOffset start, DateTime todayLocalDate)
        {
            try
            {
                TimeZoneInfo tz;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"); }
                catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                var local = TimeZoneInfo.ConvertTime(start, tz).Date;
                return local == todayLocalDate.Date;
            }
            catch
            {
                return start.Date == todayLocalDate.Date;
            }
        }

        private static string ReadEventDateTime(JsonElement ev, string prop)
        {
            if (!ev.TryGetProperty(prop, out var obj)) return "unknown";
            if (obj.TryGetProperty("dateTime", out var dt))
                return dt.GetString() ?? "unknown";
            if (obj.TryGetProperty("date", out var d))
                return d.GetString() ?? "unknown";
            return "unknown";
        }

        private async Task<StoredToken?> GetOrRefreshTokenAsync(string userId)
        {
            var stored = await LoadTokenAsync(userId);
            if (stored == null) return null;

            if (!string.IsNullOrWhiteSpace(stored.AccessToken) &&
                stored.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
                return stored;

            if (string.IsNullOrWhiteSpace(stored.RefreshToken))
                return stored;

            var refreshed = await RefreshAsync(stored.RefreshToken);
            if (refreshed == null) return stored;

            stored.AccessToken = refreshed.AccessToken;
            stored.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
            if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                stored.RefreshToken = refreshed.RefreshToken;

            await SaveTokenAsync(userId, stored);
            return stored;
        }

        private async Task<StoredToken?> LoadTokenAsync(string userId)
        {
            var connStr = SqlConnectionFactory.DefaultConnection(_config);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT TOP 1 AccessToken, RefreshToken, ExpiresAtUtc
                FROM dbo.GoogleCalendarTokens
                WHERE UserId = @uid
                ORDER BY Id DESC", conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new StoredToken
            {
                AccessToken = reader.IsDBNull(0) ? null : reader.GetString(0),
                RefreshToken = reader.IsDBNull(1) ? null : reader.GetString(1),
                ExpiresAtUtc = reader.GetDateTime(2)
            };
        }

        private async Task SaveTokenAsync(string userId, StoredToken token)
        {
            var connStr = SqlConnectionFactory.DefaultConnection(_config);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                UPDATE dbo.GoogleCalendarTokens
                SET AccessToken = @a, RefreshToken = @r, ExpiresAtUtc = @e
                WHERE UserId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@a", (object?)token.AccessToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@r", (object?)token.RefreshToken ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", token.ExpiresAtUtc);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<RefreshResponse?> RefreshAsync(string refreshToken)
        {
            var clientId = _config["Authentication:Google:ClientId"];
            var clientSecret = _config["Authentication:Google:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return null;

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }!);

            using var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;

            return JsonSerializer.Deserialize<RefreshResponse>(json);
        }

        private sealed class StoredToken
        {
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
        }

        private sealed class RefreshResponse
        {
            public string? access_token { get; set; }
            public int expires_in { get; set; }
            public string? refresh_token { get; set; }

            public string? AccessToken => access_token;
            public int ExpiresIn => expires_in;
            public string? RefreshToken => refresh_token;
        }
    }
}
