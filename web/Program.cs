using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// DB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

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
builder.Services.AddDistributedMemoryCache();

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
