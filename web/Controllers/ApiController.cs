using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SpotifyStatisticsWebApp.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SpotifyStatisticsWebApp.Controllers
{
    /// <summary>
    /// REST API consumed by the Statify iOS app.
    /// All endpoints except /auth/login require a valid JWT Bearer token.
    ///
    /// Base URL: /api
    ///
    /// Auth flow:
    ///   1. POST /api/auth/login  { email, password }  → { token, expiresAt }
    ///   2. Attach header to every subsequent request:
    ///      Authorization: Bearer {token}
    /// </summary>
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public ApiController(
            IConfiguration config,
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _config      = config;
            _db          = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Opens a connection to the music history SQL Server database.</summary>
        private async Task<SqlConnection> MusicDbAsync()
        {
            var conn = new SqlConnection(_config.GetConnectionString("MusicHistoryConnection"));
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>Opens a connection to the main (Identity / Spotify tokens) database.</summary>
        private async Task<SqlConnection> DefaultDbAsync()
        {
            var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>Executes a scalar SQL query and returns the result cast to T.</summary>
        private static async Task<T> ScalarAsync<T>(SqlConnection conn, string sql, params (string, object)[] parameters)
        {
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value);
            var result = await cmd.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result ?? 0, typeof(T));
        }

        /// <summary>
        /// Generates a JWT token for the given user.
        /// Secret is read from JWT_SECRET environment variable (Railway → Variables).
        /// Token expires after 30 days so iOS users don't get logged out frequently.
        /// </summary>
        private string GenerateJwt(IdentityUser user)
        {
            var secret  = _config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET not set");
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddDays(30);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer:   "statify",
                audience: "statify-ios",
                claims:   claims,
                expires:  expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Returns the authenticated user's Identity ID from the JWT claims.</summary>
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException("User ID not found in token");

        /// <summary>
        /// Builds a SQL date filter clause based on the range query parameter.
        /// Matches the same logic used in Dashboard.cshtml.cs.
        /// </summary>
        private static string DateFilter(string range) => range switch
        {
            "7d"  => "AND played_at >= DATEADD(day,   -7,  GETUTCDATE())",
            "30d" => "AND played_at >= DATEADD(day,   -30, GETUTCDATE())",
            "6m"  => "AND played_at >= DATEADD(month, -6,  GETUTCDATE())",
            _     => ""
        };

        // ══════════════════════════════════════════════════════════════════════
        // AUTH
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/auth/login
        /// Authenticates a user with email + password and returns a JWT token.
        /// Only works for accounts that have a local password (not OAuth-only).
        ///
        /// Request body: { "email": "...", "password": "..." }
        /// Response:     { "token": "...", "expiresAt": "..." }
        /// </summary>
        [HttpPost("auth/login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Email and password are required" });

            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user == null)
                return Unauthorized(new { error = "Invalid email or password" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Unauthorized(new { error = "Invalid email or password" });

            var token   = GenerateJwt(user);
            var expires = DateTime.UtcNow.AddDays(30);

            return Ok(new
            {
                token,
                expiresAt = expires.ToString("o"),
                email     = user.Email,
            });
        }

        // <summary>
        // POST /api/auth/register
        // Creates a new account and returns a JWT token immediately.
        // Request body: { "email": "...", "password": "..." }
        // Response:     { "token": "...", "expiresAt": "...", "email": "..." }
        // </summary>
        [HttpPost("auth/register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Email and password are required" });
        
            // Check if email already exists
            var existing = await _userManager.FindByEmailAsync(req.Email);
            if (existing != null)
                return Conflict(new { error = "An account with this email already exists" });
        
            // Create the new user
            var user = new IdentityUser
            {
                UserName = req.Email,
                Email = req.Email,
                EmailConfirmed = true
            };
        
            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { error = string.Join(" ", errors) });
            }
        
            // Generate JWT immediately so user is logged in after registration
            var token   = GenerateJwt(user);
            var expires = DateTime.UtcNow.AddDays(30);
        
            return Ok(new
            {
                token,
                expiresAt = expires.ToString("o"),
                email     = user.Email,
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROFILE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/profile
        /// Returns the current user's display name, email, avatar, and Spotify connection status.
        /// </summary>
        [HttpGet("profile")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = UserId;
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            await using var defaultDb = await DefaultDbAsync();
            var spotifyConnected = await ScalarAsync<int>(defaultDb,
                "SELECT COUNT(*) FROM SpotifyTokens WHERE UserId = @uid",
                ("@uid", userId)) > 0;

            return Ok(new
            {
                displayName      = profile?.DisplayName,
                email            = User.FindFirstValue(JwtRegisteredClaimNames.Email),
                avatarBase64     = profile?.AvatarBase64,
                spotifyConnected,
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // SETTINGS — update profile fields from iOS app
        // ══════════════════════════════════════════════════════════════════════

        public record UpdateProfileRequest(string? DisplayName);
        public record UpdatePhoneRequest(string? PhoneNumber);
        public record UpdateAvatarRequest(string? AvatarBase64);

        /// <summary>
        /// PUT /api/settings/profile
        /// Updates the user's display name.
        /// Request body: { "displayName": "..." }
        /// </summary>
        [HttpPut("settings/profile")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            if (req.DisplayName != null && req.DisplayName.Length > 100)
                return BadRequest(new { error = "Display name must be 100 characters or fewer." });

            var userId = UserId;
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                profile = new UserProfile { UserId = userId };
                _db.UserProfiles.Add(profile);
            }

            profile.DisplayName = req.DisplayName?.Trim();
            await _db.SaveChangesAsync();
            return Ok(new { displayName = profile.DisplayName });
        }

        /// <summary>
        /// PUT /api/settings/phone
        /// Updates the user's phone number.
        /// Request body: { "phoneNumber": "..." }
        /// </summary>
        [HttpPut("settings/phone")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequest req)
        {
            var userId = UserId;
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                profile = new UserProfile { UserId = userId };
                _db.UserProfiles.Add(profile);
            }

            profile.PhoneNumber = req.PhoneNumber?.Trim();
            await _db.SaveChangesAsync();
            return Ok(new { phoneNumber = profile.PhoneNumber });
        }


        /// <summary>
        /// PUT /api/settings/avatar
        /// Saves a base64-encoded avatar image.
        /// Request body: { "avatarBase64": "data:image/jpeg;base64,..." }
        /// </summary>
        [HttpPut("settings/avatar")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest req)
        {
            var userId = UserId;
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                profile = new UserProfile { UserId = userId };
                _db.UserProfiles.Add(profile);
            }

            profile.AvatarBase64 = req.AvatarBase64;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
        // ══════════════════════════════════════════════════════════════════════
        // DASHBOARD STATS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/stats?range=all|7d|30d|6m
        /// Returns summary counts and top 10 lists for the dashboard.
        /// Mirrors the data loaded by Dashboard.cshtml.cs.
        /// </summary>
        [HttpGet("stats")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetStats([FromQuery] string range = "all")
        {
            if (range is not ("7d" or "30d" or "6m" or "all")) range = "all";
            var userId = UserId;
            var df = DateFilter(range);

            await using var conn = await MusicDbAsync();

            var totalTracks    = await ScalarAsync<int>(conn, $"SELECT COUNT(*) FROM music_history WHERE user_id = @uid {df}", ("@uid", userId));
            var uniqueArtists  = await ScalarAsync<int>(conn, $"SELECT COUNT(DISTINCT artist) FROM music_history WHERE user_id = @uid {df}", ("@uid", userId));
            var uniqueAlbums   = await ScalarAsync<int>(conn, $"SELECT COUNT(DISTINCT album) FROM music_history WHERE user_id = @uid {df}", ("@uid", userId));
            var uniqueCountries = await ScalarAsync<int>(conn, $"SELECT COUNT(DISTINCT country) FROM music_history WHERE user_id = @uid AND country != 'unknown' {df}", ("@uid", userId));

            // Top 10 tracks
            var topTracks = new List<object>();
            using (var cmd = new SqlCommand($@"
                SELECT TOP 10 song, artist, COUNT(*) as cnt
                FROM music_history WHERE user_id = @uid AND song IS NOT NULL {df}
                GROUP BY song, artist ORDER BY cnt DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    topTracks.Add(new { name = r.GetString(0), artist = r.GetString(1), count = r.GetInt32(2) });
            }

            // Top 10 artists
            var topArtists = new List<object>();
            using (var cmd = new SqlCommand($@"
                SELECT TOP 10 artist, country, COUNT(*) as cnt
                FROM music_history WHERE user_id = @uid AND artist IS NOT NULL {df}
                GROUP BY artist, country ORDER BY cnt DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    topArtists.Add(new { name = r.GetString(0), country = r.GetString(1), count = r.GetInt32(2) });
            }

            // Top 10 albums
            var topAlbums = new List<object>();
            using (var cmd = new SqlCommand($@"
                SELECT TOP 10 album, artist, COUNT(*) as cnt
                FROM music_history WHERE user_id = @uid AND album IS NOT NULL {df}
                GROUP BY album, artist ORDER BY cnt DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    topAlbums.Add(new { name = r.GetString(0), artist = r.GetString(1), count = r.GetInt32(2) });
            }

            return Ok(new
            {
                totalTracks,
                uniqueArtists,
                uniqueAlbums,
                uniqueCountries,
                topTracks,
                topArtists,
                topAlbums,
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // TIME OF DAY
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/timeofday?range=all|7d|30d|6m
        /// Returns play counts for each hour of the day (0-23).
        /// Used to render the 24-bar equaliser chart on the dashboard.
        /// </summary>
        [HttpGet("timeofday")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetTimeOfDay([FromQuery] string range = "all")
        {
            if (range is not ("7d" or "30d" or "6m" or "all")) range = "all";
            var userId = UserId;
            var df = DateFilter(range);

            // Initialise all 24 hours to 0 so iOS doesn't have to handle missing hours
            var hours = new int[24];

            await using var conn = await MusicDbAsync();
            using var cmd = new SqlCommand($@"
                SELECT DATEPART(HOUR, played_at) as hr, COUNT(*) as cnt
                FROM music_history WHERE user_id = @uid {df}
                GROUP BY DATEPART(HOUR, played_at)", conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int hr = reader.GetInt32(0);
                if (hr >= 0 && hr < 24)
                    hours[hr] = reader.GetInt32(1);
            }

            return Ok(new { hours });
        }

        // ══════════════════════════════════════════════════════════════════════
        // ACTIVITY BY MONTH
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/activity?range=all|7d|30d|6m
        /// Returns monthly play counts ordered chronologically.
        /// Used to render the activity line chart on the dashboard.
        /// </summary>
        [HttpGet("activity")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetActivity([FromQuery] string range = "all")
        {
            if (range is not ("7d" or "30d" or "6m" or "all")) range = "all";
            var userId = UserId;
            var df = DateFilter(range);

            var months = new List<object>();

            await using var conn = await MusicDbAsync();
            using var cmd = new SqlCommand($@"
                SELECT FORMAT(played_at, 'MMM yyyy') as mo, COUNT(*) as cnt
                FROM music_history WHERE user_id = @uid {df}
                GROUP BY FORMAT(played_at, 'MMM yyyy'), YEAR(played_at), MONTH(played_at)
                ORDER BY YEAR(played_at), MONTH(played_at)", conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                months.Add(new { month = reader.GetString(0), count = reader.GetInt32(1) });

            return Ok(new { months });
        }

        // ══════════════════════════════════════════════════════════════════════
        // RECENTLY PLAYED
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/history?page=1&amp;limit=50
        /// Returns paginated listening history ordered by most recent.
        /// Default page size is 50, max is 100.
        /// Response includes pagination metadata so iOS can implement infinite scroll.
        /// </summary>
        [HttpGet("history")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int limit = 50)
        {
            // Clamp inputs to safe ranges
            page  = Math.Max(1, page);
            limit = Math.Clamp(limit, 1, 100);

            var userId = UserId;
            await using var conn = await MusicDbAsync();

            // Total count for pagination metadata
            var totalCount = await ScalarAsync<int>(conn,
                "SELECT COUNT(*) FROM music_history WHERE user_id = @uid",
                ("@uid", userId));

            int totalPages = (int)Math.Ceiling(totalCount / (double)limit);
            int offset     = (page - 1) * limit;

            var tracks = new List<object>();
            using var cmd = new SqlCommand(@"
                SELECT song, artist, album, country, played_at
                FROM music_history
                WHERE user_id = @uid
                ORDER BY played_at DESC
                OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@size", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tracks.Add(new
                {
                    song     = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    artist   = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    album    = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    country  = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    // played_at is stored as Toronto local time — attach correct EDT/EST offset
                    playedAt = reader.IsDBNull(4) ? "" : FormatTorontoTime(reader.GetDateTime(4)),
                });

            return Ok(new
            {
                page,
                limit,
                totalCount,
                totalPages,
                hasNextPage = page < totalPages,
                tracks,
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // WORLD MAP
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/map
        /// Returns play counts per country, excluding unknown entries.
        /// Used to render the world map visualization on the map screen.
        /// </summary>
        [HttpGet("map")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetMap()
        {
            var userId = UserId;
            var countries = new List<object>();

            await using var conn = await MusicDbAsync();
            using var cmd = new SqlCommand(@"
                SELECT country, COUNT(*) as cnt
                FROM music_history
                WHERE user_id = @uid AND country IS NOT NULL AND country != 'unknown'
                GROUP BY country
                ORDER BY cnt DESC", conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                countries.Add(new
                {
                    country = reader.GetString(0),
                    count   = reader.GetInt32(1),
                });

            return Ok(new { countries });
        }
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────

    /// <summary>Request body for POST /api/auth/login</summary>
    public class LoginRequest
    {
        public string Email    { get; set; } = "";
        public string Password { get; set; } = "";
    }
    /// <summary>
    /// Formats a Toronto local datetime as ISO-8601 with the correct EDT/EST offset.
    /// e.g. "2026-05-06T09:39:00-04:00"
    /// </summary>
    private static string FormatTorontoTime(DateTime dt)
    {
        TimeZoneInfo tz;
        try   { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }

        var dtUtc  = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), tz);
        var offset = dt - dtUtc;
        var sign   = offset >= TimeSpan.Zero ? "+" : "-";
        var hhmm   = offset.Duration().ToString(@"hh\:mm");
        return dt.ToString("yyyy-MM-ddTHH:mm:ss") + sign + hhmm;
    }
}
