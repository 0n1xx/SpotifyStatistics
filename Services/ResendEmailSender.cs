using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Services
{
    /// <summary>
    /// IEmailSender implementation через Resend HTTP API.
    /// API key читается из переменной окружения RESEND_API_KEY (Railway → Variables).
    /// From address: onboarding@resend.dev (работает без верификации домена).
    /// Когда добавишь свой домен в Resend — замени FromAddress на свой.
    /// </summary>
    public class ResendEmailSender : IEmailSender
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ResendEmailSender> _logger;

        // Отправитель — пока используем тестовый адрес Resend.
        // Письма будут доходить только на твой собственный email пока домен не верифицирован.
        private const string FromAddress = "noreply@statify.one";
        private const string FromName    = "Statify";

        public ResendEmailSender(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<ResendEmailSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config            = config;
            _logger            = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _config["RESEND_API_KEY"]
                ?? throw new InvalidOperationException(
                    "RESEND_API_KEY is not set. Add it in Railway → Variables.");

            // Собираем payload для Resend API
            var payload = new
            {
                from    = $"{FromName} <{FromAddress}>",
                to      = new[] { email },
                subject = subject,
                html    = htmlMessage
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient("resend");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await client.PostAsync(
                "https://api.resend.com/emails", content);

            if (!response.IsSuccessStatusCode)
            {
                // Логируем ошибку но не бросаем исключение — 
                // пользователь видит страницу confirmation в любом случае (security best practice)
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Resend API error {Status}: {Body}",
                    (int)response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation(
                    "Password reset email sent via Resend to {Email}", email);
            }
        }
    }
}
