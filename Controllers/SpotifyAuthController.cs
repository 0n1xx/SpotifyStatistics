using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpotifyStatisticsWebApp.Data;
using SpotifyStatisticsWebApp.Models;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class SpotifyAuthController : Controller
    {
        private readonly SpotifySettings _spotify;
        private readonly HttpClient _http;
        private readonly ApplicationDbContext _db;

        public SpotifyAuthController(
            IOptions<SpotifySettings> spotify,
            IHttpClientFactory factory,
            ApplicationDbContext db)
        {
            _spotify = spotify.Value;
            _http = factory.CreateClient();
            _db = db;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var state = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("spotify_state", state);

            var scopes = Uri.EscapeDataString(
                "user-read-recently-played user-top-read user-read-playback-state");

            var redirectUri = Uri.EscapeDataString(_spotify.RedirectUri);

            var url = "https://accounts.spotify.com/authorize" +
                      $"?client_id={_spotify.ClientId}" +
                      $"&response_type=code" +
                      $"&redirect_uri={redirectUri}" +
                      $"&scope={scopes}" +
                      $"&state={state}";

            return Redirect(url);
        }

        [HttpGet("/callback")]
        public async Task<IActionResult> Callback(string code, string state, string error = null)
        {
            if (error != null)
                return BadRequest($"Spotify error: {error}");

            var savedState = HttpContext.Session.GetString("spotify_state");
            if (state != savedState)
                return BadRequest("State mismatch");

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_spotify.ClientId}:{_spotify.ClientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://accounts.spotify.com/api/token");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "authorization_code"),
                new KeyValuePair<string,string>("code", code),
                new KeyValuePair<string,string>("redirect_uri", _spotify.RedirectUri),
            });

            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var tokens = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var existing = await _db.SpotifyTokens
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (existing != null)
            {
                existing.AccessToken  = tokens.access_token;
                existing.RefreshToken = tokens.refresh_token;
                existing.ExpiresAt    = DateTime.UtcNow.AddSeconds(tokens.expires_in);
            }
            else
            {
                _db.SpotifyTokens.Add(new SpotifyToken
                {
                    UserId        = userId,
                    AccessToken   = tokens.access_token,
                    RefreshToken  = tokens.refresh_token,
                    ExpiresAt     = DateTime.UtcNow.AddSeconds(tokens.expires_in)
                });
            }

            await _db.SaveChangesAsync();
            return Redirect("/Settings");
        }

        [HttpGet("disconnect")]
        public async Task<IActionResult> Disconnect()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var token  = await _db.SpotifyTokens.FirstOrDefaultAsync(t => t.UserId == userId);
            if (token != null)
            {
                _db.SpotifyTokens.Remove(token);
                await _db.SaveChangesAsync();
            }
            return Redirect("/Settings");
        }
    }
}

public record SpotifyTokenResponse(
    string access_token,
    string token_type,
    int    expires_in,
    string refresh_token,
    string scope
);
