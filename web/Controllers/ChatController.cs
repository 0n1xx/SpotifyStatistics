using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Data;
using SpotifyStatisticsWebApp.Services;
using System.Security.Claims;
using System.Text;

namespace SpotifyStatisticsWebApp.Controllers
{
    // Only logged-in users can use chat.
    // Uses cookie auth from the website (not the iOS Bearer token).
    [Authorize]
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly OpenAIService _openAI;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _config;

        public ChatController(
            OpenAIService openAI,
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IConfiguration config)
        {
            _openAI = openAI;
            _db = db;
            _userManager = userManager;
            _config = config;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
        }

        public class ChatResponse
        {
            public string Reply { get; set; } = "";
        }

        // POST /api/chat
        [HttpPost]
        public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new ChatResponse { Reply = "Message is empty." });

            // Get CURRENT logged-in user id from auth.
            // Do not trust any "userId" coming from the chat text.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ChatResponse { Reply = "Not logged in." });

            var user = await _userManager.FindByIdAsync(userId);

            // Load ONLY this user's profile from DB
            var profile = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            // Load ONLY this user's listening stats from music_history
            var listeningContext = await BuildListeningContextAsync(userId);

            // Private context for the assistant.
            // The assistant must ONLY answer about "this user".
            var profileContext =
                "Current user profile (private, only for this authenticated user):\n" +
                $"- UserId: {userId}\n" +
                $"- Email: {user?.Email ?? "unknown"}\n" +
                $"- DisplayName: {profile?.DisplayName ?? "not set"}\n" +
                $"- PhoneNumber: {profile?.PhoneNumber ?? "not set"}\n" +
                "\n" + listeningContext + "\n" +
                "Rules:\n" +
                "- Answer ONLY about THIS user using the data above.\n" +
                "- If asked about another user (any other name, email, or id), refuse.\n" +
                "- If the listening data is empty, say you have no listening history for this account yet.\n" +
                "- Do not invent artists, tracks, or counts that are not listed above.";

            var reply = await _openAI.AskAsync(
                userMessage: request.Message.Trim(),
                profileContext: profileContext);

            return Ok(new ChatResponse { Reply = reply });
        }

        // Builds a short text summary of THIS user's Spotify stats for ChatGPT.
        // Always filters by @uid so another user's rows never leak in.
        private async Task<string> BuildListeningContextAsync(string userId)
        {
            var connStr = SqlConnectionFactory.DefaultConnection(_config);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var totalTracks = await ScalarAsync<int>(conn,
                "SELECT COUNT(*) FROM dbo.music_history WHERE user_id = @uid", userId);

            var uniqueArtists = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT artist) FROM dbo.music_history WHERE user_id = @uid", userId);

            var uniqueAlbums = await ScalarAsync<int>(conn,
                "SELECT COUNT(DISTINCT album) FROM dbo.music_history WHERE user_id = @uid", userId);

            var spotifyConnected = await ScalarAsync<int>(conn,
                "SELECT COUNT(*) FROM dbo.SpotifyTokens WHERE UserId = @uid", userId) > 0;

            var topArtists = await QueryNameCountAsync(conn,
                @"SELECT TOP 5 artist, COUNT(*) as cnt
                  FROM dbo.music_history
                  WHERE user_id = @uid AND artist IS NOT NULL
                  GROUP BY artist
                  ORDER BY cnt DESC", userId);

            var topTracks = await QueryNameCountAsync(conn,
                @"SELECT TOP 5 song + ' — ' + ISNULL(artist, 'unknown'), COUNT(*) as cnt
                  FROM dbo.music_history
                  WHERE user_id = @uid AND song IS NOT NULL
                  GROUP BY song, artist
                  ORDER BY cnt DESC", userId);

            var recentTracks = await QueryNamesAsync(conn,
                @"SELECT TOP 5 ISNULL(song, 'unknown') + ' — ' + ISNULL(artist, 'unknown')
                  FROM dbo.music_history
                  WHERE user_id = @uid
                  ORDER BY played_at DESC", userId);

            var sb = new StringBuilder();
            sb.AppendLine("Current user Spotify listening data (from music_history, filtered by this user only):");
            sb.AppendLine($"- SpotifyConnected: {(spotifyConnected ? "yes" : "no")}");
            sb.AppendLine($"- TotalTracksPlayed: {totalTracks}");
            sb.AppendLine($"- UniqueArtists: {uniqueArtists}");
            sb.AppendLine($"- UniqueAlbums: {uniqueAlbums}");

            sb.AppendLine("- TopArtists (favorite / most played):");
            if (topArtists.Count == 0) sb.AppendLine("  (none)");
            else
                foreach (var (name, count) in topArtists)
                    sb.AppendLine($"  - {name} ({count} plays)");

            sb.AppendLine("- TopTracks:");
            if (topTracks.Count == 0) sb.AppendLine("  (none)");
            else
                foreach (var (name, count) in topTracks)
                    sb.AppendLine($"  - {name} ({count} plays)");

            sb.AppendLine("- RecentTracks:");
            if (recentTracks.Count == 0) sb.AppendLine("  (none)");
            else
                foreach (var name in recentTracks)
                    sb.AppendLine($"  - {name}");

            return sb.ToString();
        }

        private static async Task<T> ScalarAsync<T>(SqlConnection conn, string sql, string userId)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            var result = await cmd.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result ?? 0, typeof(T));
        }

        private static async Task<List<(string Name, int Count)>> QueryNameCountAsync(
            SqlConnection conn, string sql, string userId)
        {
            var list = new List<(string, int)>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add((reader.GetString(0), reader.GetInt32(1)));
            return list;
        }

        private static async Task<List<string>> QueryNamesAsync(
            SqlConnection conn, string sql, string userId)
        {
            var list = new List<string>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(reader.GetString(0));
            return list;
        }
    }
}
