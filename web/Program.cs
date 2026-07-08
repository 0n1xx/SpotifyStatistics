using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SpotifyStatisticsWebApp.Data;
using SpotifyStatisticsWebApp.Models;
using SpotifyStatisticsWebApp.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Railway injects PORT — listen on the same port the proxy targets
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// DB — external hosts (e.g. databaseasp.net) use a self-signed TLS cert
var connectionString = SqlConnectionFactory.DefaultConnection(builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
           .ConfigureWarnings(w => w.Ignore(
               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("SpotifyStatisticsWebApp");

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = "Google";
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

    // Google Calendar integration (read-only).
    // We request offline access so we can refresh tokens server-side.
    options.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
    options.SaveTokens = true;

    // Force refresh_token to be issued (Google issues it only on first consent).
    options.AccessType = "offline";
    options.Prompt = "consent";

    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            // Persist tokens per logged-in user so chat can read their calendar.
            // This stores ONLY the current user's tokens (no cross-user access).
            var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return;

            var accessToken = context.AccessToken;
            var refreshToken = context.RefreshToken; // may be null if Google didn't re-issue it
            var expiresAtUtc = DateTime.UtcNow.AddSeconds(context.ExpiresIn ?? 0);

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var existing = await db.GoogleCalendarTokens.FirstOrDefaultAsync(t => t.UserId == userId);
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
                    UserId = userId,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAtUtc = expiresAtUtc
                });
            }

            await db.SaveChangesAsync();
        }
    };
})
.AddGitHub(options =>
{
    options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
    options.CallbackPath = "/signin-github";
});

// MVC
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// ── JWT Bearer — used by the iOS app API ──────────────────────────────────
// Secret is read from JWT_SECRET environment variable (Railway → Variables).
// The Bearer scheme runs alongside Cookie auth — web pages use cookies,
// iOS API calls use Bearer tokens. They don't interfere with each other.
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "dev-secret-change-in-production";
builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = "statify",
            ValidAudience            = "statify-ios",
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

// Utils
builder.Services.AddHttpClient();
builder.Services.AddScoped<SpotifyStatisticsWebApp.Services.JwtService>();
builder.Services.AddDistributedMemoryCache();

// Ask Statify chat
// Registers OpenAIService so ChatController can call OpenAI via DI.
// The API key should be provided as Railway variable OpenAI__ApiKey.
builder.Services.AddHttpClient<OpenAIService>();

// Google Calendar (read-only) service
builder.Services.AddHttpClient<GoogleCalendarService>();

// Email sender — Resend API (key: Railway → Variables → RESEND_API_KEY)
builder.Services.AddTransient<IEmailSender, ResendEmailSender>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Spotify config
builder.Services.Configure<SpotifySettings>(
    builder.Configuration.GetSection("Spotify"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var retries = 10;
    while (retries-- > 0)
    {
        try
        {
            db.Database.Migrate();

            // UserProfiles may exist from manual SQL setup without DisplayName
            db.Database.ExecuteSqlRaw(@"
                IF COL_LENGTH('UserProfiles', 'DisplayName') IS NULL
                    ALTER TABLE UserProfiles ADD DisplayName NVARCHAR(100) NULL;
            ");

            // Google Calendar tokens table (if not using migrations)
            db.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.GoogleCalendarTokens', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.GoogleCalendarTokens (
                        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        UserId NVARCHAR(450) NULL,
                        AccessToken NVARCHAR(MAX) NULL,
                        RefreshToken NVARCHAR(MAX) NULL,
                        ExpiresAtUtc DATETIME2 NOT NULL
                    );
                END
            ");
            break;
        }
        catch (Exception ex) when (ex.Message.Contains("already an object") || ex.Message.Contains("already exists"))
        {
            // Tables exist but migrations table is empty - just insert migration records
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '00000000000000_CreateIdentitySchema')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('00000000000000_CreateIdentitySchema', '9.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260321185315_InitialCreate')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('20260321185315_InitialCreate', '9.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260321191243_AddSpotifyTokens')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('20260321191243_AddSpotifyTokens', '9.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260424005330_InitialCreate')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('20260424005330_InitialCreate', '9.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260424011043_DataProtectionKeys')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('20260424011043_DataProtectionKeys', '9.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260427000000_AddUserProfiles')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('20260427000000_AddUserProfiles', '9.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260428000000_AddDisplayName')
                    INSERT INTO [__EFMigrationsHistory] VALUES ('20260428000000_AddDisplayName', '9.0.0');
                IF COL_LENGTH('UserProfiles', 'DisplayName') IS NULL
                    ALTER TABLE UserProfiles ADD DisplayName NVARCHAR(100) NULL;
            ");
            break;
        }
        catch (Exception)
        {
            if (retries == 0) throw;
            Thread.Sleep(3000);
        }
    }
}

// Forwarded headers 
var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost
};
fwdOptions.KnownNetworks.Clear();
fwdOptions.KnownProxies.Clear();

app.UseForwardedHeaders(fwdOptions);

// Errors
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Pipeline
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapRazorPages();

// Map API controllers — required for ApiController to respond to /api/* routes
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
