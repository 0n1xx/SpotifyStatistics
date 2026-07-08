using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Services
{
    // Minimal OpenAI Chat Completions wrapper for the "Ask Statify" widget.
    // Reads API key from config key "OpenAI:ApiKey" (Railway usually provides OpenAI__ApiKey).
    public class OpenAIService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public OpenAIService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<string> AskAsync(string userMessage, string? profileContext = null)
        {
            var apiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return "OpenAI API key is missing. Set OpenAI:ApiKey in config.";

            var systemPrompt =
                "You are Ask Statify, a helpful assistant for a Spotify statistics app. " +
                "Be short and friendly. " +
                "You may use the provided current-user profile context. " +
                "Never invent other users' data. " +
                "If asked about another person's account, refuse.";

            if (!string.IsNullOrWhiteSpace(profileContext))
                systemPrompt += "\n\n" + profileContext;

            var body = new
            {
                model = "gpt-4o-mini",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            using var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"OpenAI error: {(int)response.StatusCode}";

            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? "Empty response from OpenAI."
                : content.Trim();
        }
    }
}

