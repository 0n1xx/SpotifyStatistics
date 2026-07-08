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

        public async Task<string> GetUpcomingSummaryAsync(string userId, int maxEvents = 5, string? sharedCalendarName = null)
        {
            // Ensure we have a valid access token for this user
            var token = await GetOrRefreshTokenAsync(userId);
            if (token == null)
                return "Google Calendar is not connected for this account yet. Log out and sign in with Google again to grant calendar access.";

            var timeMin = Uri.EscapeDataString(DateTime.UtcNow.ToString("o"));
            var sources = new List<(string CalendarId, string Label)>
            {
                ("primary", "Primary")
            };

            if (!string.IsNullOrWhiteSpace(sharedCalendarName))
            {
                var sharedId = await FindCalendarIdByNameAsync(token.AccessToken!, sharedCalendarName);
                if (!string.IsNullOrWhiteSpace(sharedId))
                    sources.Add((sharedId, sharedCalendarName));
            }

            var allEvents = new List<(string Start, string End, string Summary, string Source)>();
            foreach (var (calendarId, label) in sources)
            {
                var url =
                    $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events" +
                    $"?timeMin={timeMin}" +
                    "&singleEvents=true&orderBy=startTime" +
                    $"&maxResults={maxEvents}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                using var resp = await _http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return $"Google Calendar API error: {(int)resp.StatusCode}";

                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.TryGetProperty("items", out var arr) ? arr : default;
                if (items.ValueKind != JsonValueKind.Array) continue;

                foreach (var ev in items.EnumerateArray())
                {
                    var summary = ev.TryGetProperty("summary", out var s) ? s.GetString() : "(no title)";
                    var start = ReadEventDateTime(ev, "start");
                    var end = ReadEventDateTime(ev, "end");
                    allEvents.Add((start, end, summary ?? "(no title)", label));
                }
            }

            if (allEvents.Count == 0)
                return "Google Calendar: no upcoming events found.";

            // Sort by start (string ISO) and take top N
            var upcoming = allEvents
                .OrderBy(e => e.Start)
                .Take(maxEvents)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Google Calendar (upcoming events):");
            foreach (var ev in upcoming)
                sb.AppendLine($"- [{ev.Source}] {ev.Summary} — {ev.Start} to {ev.End}");

            return sb.ToString();
        }

        private async Task<string?> FindCalendarIdByNameAsync(string accessToken, string calendarName)
        {
            // List calendars visible to this account and match by summary (display name)
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/users/me/calendarList");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in items.EnumerateArray())
            {
                var summary = item.TryGetProperty("summary", out var s) ? s.GetString() : null;
                if (string.Equals(summary, calendarName, StringComparison.OrdinalIgnoreCase))
                {
                    return item.TryGetProperty("id", out var id) ? id.GetString() : null;
                }
            }

            return null;
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

            // if still valid for 2 minutes, use it
            if (!string.IsNullOrWhiteSpace(stored.AccessToken) &&
                stored.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
                return stored;

            // refresh if possible
            if (string.IsNullOrWhiteSpace(stored.RefreshToken))
                return stored; // can't refresh; caller may get 401 and need re-consent

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

