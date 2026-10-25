using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Services
{
    // Thin wrapper around the Telegram Bot API.
    // Bot token comes from config "Telegram:BotToken" (Railway: Telegram__BotToken).
    public class TelegramService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public TelegramService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        private string? BotToken => _config["Telegram:BotToken"];

        public async Task SendMessageAsync(long chatId, string text, CancellationToken ct = default)
        {
            var token = BotToken;
            if (string.IsNullOrWhiteSpace(token)) return;

            var payload = new { chat_id = chatId, text };
            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            await _http.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", content, ct);
        }
    }
}
