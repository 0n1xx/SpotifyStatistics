# Statify — Web

> Dark-themed web application built with ASP.NET Core and Razor Pages. Continuously synced with the Airflow data pipeline, deployed on Railway at [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app).

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 |
| Language | C# |
| Pages | Razor Pages |
| ORM | Entity Framework Core |
| App database | Microsoft SQL Server |
| Auth | ASP.NET Identity · Google OAuth · GitHub OAuth · Spotify OAuth |
| Frontend | Vanilla JS · Custom CSS |
| Maps | D3.js |
| Email | Resend API (`noreply@statify.one`) |
| Hosting | Railway |

---

## Project Structure

```
web/
├── Areas/Identity/             # ASP.NET Identity — auth + full account management
│   └── Pages/Account/
│       ├── Login.cshtml / Register.cshtml
│       ├── ForgotPassword / ResetPassword
│       └── Manage/             # Profile, Email, Password, Linked accounts, GDPR
│
├── Controllers/
│   ├── SpotifyAuthController.cs    # Spotify OAuth flow (PKCE)
│   └── ApiController.cs            # REST endpoints for iOS app
│
├── Data/
│   └── ApplicationDbContext.cs     # EF Core context
│
├── Migrations/                     # EF Core schema migrations
│
├── Models/                         # C# data models
│
├── Pages/
│   ├── Dashboard.cshtml            # Top tracks, artists, albums, charts
│   ├── RecentlyPlayed.cshtml       # Paginated history with search
│   ├── Worldmap.cshtml             # D3.js geographic visualization
│   └── Settings.cshtml             # Full account management
│
├── Services/
│   └── ResendEmailSender.cs        # Transactional email via Resend
│
├── wwwroot/
│   ├── css/                        # Per-page stylesheets
│   └── js/                         # Per-page scripts
│
└── Program.cs                      # App configuration and service registration
```

---

## Pages

| Page | Description |
|---|---|
| **Dashboard** | Top tracks, artists, albums · listening activity by hour · monthly heatmap |
| **Recently Played** | Paginated full listening history with search and time range filter (7d / 30d / 90d / All) |
| **World Map** | D3.js visualization — artist origins plotted by country with play counts |
| **Settings** | Profile photo · display name · email · password · linked accounts · GDPR export · account deletion |

---

## Authentication

| Provider | Implementation |
|---|---|
| Email / Password | ASP.NET Identity with bcrypt hashing |
| Google OAuth | `Microsoft.AspNetCore.Authentication.Google` |
| GitHub OAuth | `AspNet.Security.OAuth.GitHub` |
| Spotify OAuth | Custom `SpotifyAuthController` with PKCE flow |

All OAuth providers are linked to the same ASP.NET Identity account. Users can connect or disconnect providers from the Settings page.

---

## Email

Transactional email is sent via **Resend** on the verified domain `statify.one`:

- Password reset — forgot password → branded email → reset link → confirmation
- Sender address: `noreply@statify.one`
- Branded dark-theme HTML email template with Statify logo

---

## Design System

| Token | Value |
|---|---|
| Background | `#080808` |
| Card | `#111111` |
| Accent | `#1DB954` (Spotify green) |
| Text primary | `#FFFFFF` |
| Text secondary | `#999999` |
| Heading font | Syne |
| Body font | DM Sans |
| Responsive breakpoints | 900px (tablet) · 600px (mobile) |

No inline styles — all CSS is organized in per-page files under `wwwroot/css/`.

---

## Local Setup

### Prerequisites

- .NET 10 SDK
- Microsoft SQL Server (local instance or Docker)
- Spotify Developer App ([create at developer.spotify.com](https://developer.spotify.com))
- Google OAuth credentials ([console.cloud.google.com](https://console.cloud.google.com))
- GitHub OAuth App ([github.com/settings/applications](https://github.com/settings/applications))
- Resend account + verified domain ([resend.com](https://resend.com))

### Install & Run

```bash
cd web
dotnet restore
dotnet run
```

The app will be available at `https://localhost:5001`.

### Environment Variables

Set these in `appsettings.Development.json` or as environment variables:

```
ConnectionStrings__DefaultConnection       # SQL Server — app database (Identity, profiles)
ConnectionStrings__MusicHistoryConnection  # SQL Server — music history (synced from ClickHouse)
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Authentication__GitHub__ClientId
Authentication__GitHub__ClientSecret
Spotify__ClientId
Spotify__ClientSecret
Spotify__RedirectUri                       # e.g. https://localhost:5001/spotify/callback
RESEND_API_KEY
JWT_SECRET                                 # Used for iOS API JWT tokens
```

### Database Migrations

Apply EF Core migrations to create the schema:

```bash
cd web
dotnet ef database update
```

---

## Railway Deployment

1. Connect the GitHub repository in Railway → **Settings → Source**
2. Set the **Root Directory** to `web`
3. Add all environment variables listed above under Railway → **Variables**
4. Deploy — Railway builds and runs the app automatically on every `git push` to `main`

The production build uses `appsettings.Production.json` with Railway's injected environment variables.
