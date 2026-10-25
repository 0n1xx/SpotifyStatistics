using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Data;
using SpotifyStatisticsWebApp.Models;
using SpotifyStatisticsWebApp.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly TelegramService _telegram;
        private readonly IConfiguration _config;

        public TelegramController(ApplicationDbContext db, TelegramService telegram, IConfiguration config)
        {
            _db = db;
            _telegram = telegram;
            _config = config;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        // GET /api/telegram/status — is this user's Telegram linked?
        [Authorize(AuthenticationSchemes = "Bearer,Identity.Application")]
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var link = await _db.TelegramLinks.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == userId);
            return Ok(new
            {
                connected = link?.ChatId != null,
                weeklySummaryEnabled = link?.WeeklySummaryEnabled ?? true
            });
        }

        // POST /api/telegram/link — start/renew the link flow; returns a t.me deep link
        // that the client should open. The user taps Start in Telegram to finish linking.
        [Authorize(AuthenticationSchemes = "Bearer,Identity.Application")]
        [HttpPost("link")]
        public async Task<IActionResult> Link()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var botUsername = _config["Telegram:BotUsername"];
            if (string.IsNullOrWhiteSpace(botUsername))
                return BadRequest(new { error = "Telegram bot is not configured yet." });

            var link = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.UserId == userId);
            if (link == null)
            {
                link = new TelegramLink { UserId = userId };
                _db.TelegramLinks.Add(link);
            }

            link.LinkToken = Guid.NewGuid().ToString("N");
            link.LinkTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync();

            return Ok(new { url = $"https://t.me/{botUsername}?start={link.LinkToken}" });
        }

        // POST /api/telegram/unlink
        [Authorize(AuthenticationSchemes = "Bearer,Identity.Application")]
        [HttpPost("unlink")]
        public async Task<IActionResult> Unlink()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var link = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.UserId == userId);
            if (link != null)
            {
                link.ChatId = null;
                link.LinkToken = null;
                link.LinkTokenExpiresAtUtc = null;
                await _db.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        public class WeeklySummaryRequest { public bool Enabled { get; set; } }

        // POST /api/telegram/weekly-summary — toggle the weekly send on/off
        [Authorize(AuthenticationSchemes = "Bearer,Identity.Application")]
        [HttpPost("weekly-summary")]
        public async Task<IActionResult> SetWeeklySummary([FromBody] WeeklySummaryRequest request)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var link = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.UserId == userId);
            if (link == null) return NotFound(new { error = "Telegram is not linked yet." });

            link.WeeklySummaryEnabled = request.Enabled;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // POST /api/telegram/webhook — Telegram calls this for every bot update.
        // Verified via the secret token Telegram echoes back in a header
        // (set once via TelegramService.SetWebhookAsync).
        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var expectedSecret = _config["Telegram:WebhookSecret"];
            if (!string.IsNullOrWhiteSpace(expectedSecret))
            {
                var providedSecret = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
                if (providedSecret != expectedSecret)
                    return Unauthorized();
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            string? text = null;
            long? chatId = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("text", out var textEl))
                        text = textEl.GetString();
                    if (message.TryGetProperty("chat", out var chatEl) && chatEl.TryGetProperty("id", out var idEl))
                        chatId = idEl.GetInt64();
                }
            }
            catch (JsonException)
            {
                return Ok(); // ignore malformed updates
            }

            if (chatId == null || string.IsNullOrWhiteSpace(text))
                return Ok();

            if (text.StartsWith("/start"))
                await HandleStartAsync(chatId.Value, text);
            else if (text.StartsWith("/stop"))
                await HandleStopAsync(chatId.Value);

            return Ok();
        }

        private async Task HandleStartAsync(long chatId, string text)
        {
            var parts = text.Split(' ', 2);
            var token = parts.Length > 1 ? parts[1].Trim() : null;

            if (string.IsNullOrWhiteSpace(token))
            {
                // Already-linked user re-sending /start (e.g. after /stop) — just re-enable.
                var existing = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.ChatId == chatId);
                if (existing != null)
                {
                    existing.WeeklySummaryEnabled = true;
                    await _db.SaveChangesAsync();
                    await _telegram.SendMessageAsync(chatId, "Weekly summaries are back on.");
                }
                else
                {
                    await _telegram.SendMessageAsync(chatId,
                        "Hi! Open Statify → Settings → Connect Telegram to link this chat to your account.");
                }
                return;
            }

            var link = await _db.TelegramLinks.FirstOrDefaultAsync(l =>
                l.LinkToken == token && l.LinkTokenExpiresAtUtc > DateTime.UtcNow);

            if (link == null)
            {
                await _telegram.SendMessageAsync(chatId,
                    "That link has expired. Go back to Statify → Settings and click Connect Telegram again.");
                return;
            }

            link.ChatId = chatId;
            link.LinkToken = null;
            link.LinkTokenExpiresAtUtc = null;
            link.WeeklySummaryEnabled = true;
            await _db.SaveChangesAsync();

            await _telegram.SendMessageAsync(chatId,
                "✅ Connected! You'll get a weekly Statify listening summary here. Send /stop anytime to turn it off.");
        }

        private async Task HandleStopAsync(long chatId)
        {
            var link = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.ChatId == chatId);
            if (link == null) return;

            link.WeeklySummaryEnabled = false;
            await _db.SaveChangesAsync();
            await _telegram.SendMessageAsync(chatId,
                "Weekly summaries turned off. Send /start anytime to turn them back on.");
        }
    }
}
