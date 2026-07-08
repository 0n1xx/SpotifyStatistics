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
                "You can answer general questions like ChatGPT (general knowledge), even if they are not related to the user's stats. " +
                "You may also use the provided current-user profile and listening stats to answer user-specific questions. " +
                "If asked about an artist/band in general, you may answer with general knowledge. If you genuinely don't know, say so. " +
                "Formatting rules: " +
                "Use plain text (no markdown like **bold**). " +
                "When you return a list, put each item on its own line, prefixed with '-' or '1)'. " +
                "Keep numbers and counts on the same line as the item. " +
                "Privacy rules: " +
                "Never reveal or guess any other user's data. If asked about another person's account, refuse. " +
                "Data rules: " +
                "If a question is about the CURRENT user's personal data (profile, stats, history), use only the provided context; " +
                "if that context doesn't include the answer, say you don't have that data. " +
                "Do not invent plays/counts/listening history. " +
                "Build: chat-general-v1\n\n" +
                "Examples of questions you can answer using the provided context (user-specific):\n" +
                "- What's my display name / phone number / email?\n" +
                "- Am I Spotify connected?\n" +
                "- How many tracks have I played? Unique artists/albums/countries?\n" +
                "- Who is my favorite artist? What are my top artists / top tracks / top albums?\n" +
                "- What are my most recent tracks?\n" +
                "- What's my favorite album by <artist> (based on my history)?\n" +
                "- Summarize my listening habits in 1-2 sentences.\n" +
                "- Compare my top 2 artists.\n\n" +
                "Examples of questions you can answer with general knowledge (NOT user-specific):\n" +
                "- Tell me interesting facts about <artist>.\n" +
                "- What genre is <artist>?\n" +
                "- Recommend music similar to <artist>.\n\n" +
                "If the user asks for something user-specific that is not in the provided context, say you don't have that data in Statify yet.";

            if (!string.IsNullOrWhiteSpace(profileContext))
                systemPrompt += "\n\n" + profileContext;

            var body = new
            {
                model = "gpt-4o-mini",
                temperature = 0.3,
                max_tokens = 350,
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

