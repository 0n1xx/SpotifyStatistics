using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text;
using System.Text.Json;

namespace SpotifyStatisticsWebApp.Services
{
    /// <summary>
    /// IEmailSender implementation via Resend HTTP API.
    /// API key is read from the RESEND_API_KEY environment variable (Railway → Variables).
    /// From address uses the verified statify.one domain.
    /// </summary>
    public class ResendEmailSender : IEmailSender
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ResendEmailSender> _logger;

        // Sender address — uses verified statify.one domain so emails reach any recipient.
        private const string FromAddress = "noreply@statify.one";
        private const string FromName    = "Statify";

        // Publicly accessible logo URL — served from the app's wwwroot.
        // Email clients fetch this as an <img> tag, which works in Gmail, Outlook, Apple Mail.
        // statify_email_logo.png is a 120x120 PNG with green circle + sound wave bars.
        private const string LogoUrl =
            "https://spotifystatistics-production.up.railway.app/statify_email_logo.png";

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

            // Build the Resend API payload
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
                // Log the error but don't throw — user sees the confirmation page either way
                // (security best practice: don't reveal whether the email was sent)
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

        /// <summary>
        /// Builds the branded HTML email body for password reset.
        /// Uses inline styles and an <img> logo for maximum email client compatibility.
        /// SVG is blocked by Gmail/Outlook — PNG via <img> works everywhere.
        /// </summary>
        public static string BuildResetEmailHtml(string resetLink)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#080808;font-family:'DM Sans',Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#080808;padding:48px 0;"">
    <tr>
      <td align=""center"">
        <table width=""480"" cellpadding=""0"" cellspacing=""0""
               style=""background:#111111;border-radius:16px;border:1px solid rgba(255,255,255,0.07);overflow:hidden;"">

          <!-- Header: logo image + wordmark -->
          <tr>
            <td style=""padding:32px 40px 24px;"">
              <table cellpadding=""0"" cellspacing=""0"">
                <tr>
                  <td style=""vertical-align:middle;"">
                    <!-- PNG logo — 120x120 displayed at 36x36, crisp on retina screens -->
                    <img src=""{LogoUrl}""
                         width=""36"" height=""36""
                         alt=""Statify logo""
                         style=""display:block;border-radius:50%;border:0;"">
                  </td>
                  <td style=""padding-left:10px;vertical-align:middle;"">
                    <span style=""font-size:20px;font-weight:800;letter-spacing:-0.5px;color:#f0f0f0;"">Statify</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style=""padding:0 40px 40px;"">
              <h1 style=""font-size:22px;font-weight:700;color:#f0f0f0;margin:0 0 12px;"">
                Reset your password
              </h1>
              <p style=""font-size:14px;color:#888888;line-height:1.6;margin:0 0 28px;"">
                We received a request to reset the password for your Statify account.
                Click the button below to choose a new password.
              </p>

              <!-- CTA button -->
              <table cellpadding=""0"" cellspacing=""0"">
                <tr>
                  <td style=""background:#1DB954;border-radius:10px;"">
                    <a href=""{resetLink}""
                       style=""display:inline-block;padding:14px 28px;font-size:15px;font-weight:600;color:#000000;text-decoration:none;"">
                      Reset password
                    </a>
                  </td>
                </tr>
              </table>

              <!-- Disclaimer -->
              <p style=""font-size:12px;color:#555555;margin:28px 0 0;line-height:1.6;"">
                If you didn't request a password reset, you can safely ignore this email.
                This link expires in 24 hours.
              </p>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""padding:20px 40px;border-top:1px solid rgba(255,255,255,0.07);"">
              <p style=""font-size:12px;color:#444444;margin:0;"">
                © 2026 Statify ·
                <a href=""https://statify.one"" style=""color:#1DB954;text-decoration:none;"">statify.one</a>
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}
