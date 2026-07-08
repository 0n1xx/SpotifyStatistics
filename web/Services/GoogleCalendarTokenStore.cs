using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Data;
using SpotifyStatisticsWebApp.Models;

namespace SpotifyStatisticsWebApp.Services
{
    // Saves Google OAuth tokens under the ASP.NET Identity user id.
    // OnCreatingTicket alone is NOT enough: there the NameIdentifier is still Google's id.
    public static class GoogleCalendarTokenStore
    {
        public static async Task UpsertAsync(
            ApplicationDbContext db,
            string identityUserId,
            string? accessToken,
            string? refreshToken,
            DateTime expiresAtUtc)
        {
            if (string.IsNullOrWhiteSpace(identityUserId)) return;
            if (string.IsNullOrWhiteSpace(accessToken)) return;

            var existing = await db.GoogleCalendarTokens
                .FirstOrDefaultAsync(t => t.UserId == identityUserId);

            if (existing != null)
            {
                existing.AccessToken = accessToken;
                if (!string.IsNullOrWhiteSpace(refreshToken))
                    existing.RefreshToken = refreshToken;
                existing.ExpiresAtUtc = expiresAtUtc;
            }
            else
            {
                db.GoogleCalendarTokens.Add(new GoogleCalendarToken
                {
                    UserId = identityUserId,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAtUtc = expiresAtUtc
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
