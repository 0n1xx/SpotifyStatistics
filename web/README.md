# Statify — Backend (ASP.NET Core)

Dark-themed web application built with ASP.NET Core and Razor Pages. Deployed on Railway at [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app).

---

## Structure

```
backend/
├── Areas/Identity/         # ASP.NET Identity — auth + account management
│   └── Pages/Account/
│       ├── Login / Register
│       ├── ForgotPassword / ResetPassword
│       └── Manage/         # Profile, Email, Password, Linked accounts, GDPR
├── Controllers/
│   ├── SpotifyAuthController.cs    # Spotify OAuth flow
│   └── ApiController.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Migrations/
├── Migrations/             # EF Core migrations (UserProfiles, DataProtectionKeys)
├── Models/                 # Data models
├── Pages/                  # Razor Pages
│   ├── Dashboard.cshtml
│   ├── RecentlyPlayed.cshtml
│   ├── Worldmap.cshtml
│   └── Settings.cshtml
├── Services/
│   └── ResendEmailSender.cs        # Transactional email via Resend
├── wwwroot/
│   ├── css/                # Per-page stylesheets
│   ├── js/                 # Per-page scripts
│   └── statify_email_logo.png
└── Program.cs
```

---

## Pages

| Page | Description |
|---|---|
| Dashboard | Top tracks, artists, albums, listening by hour, activity by month |
| Recently Played | Paginated history with search and time range filter |
| World Map | D3.js geographic visualization of artists by country |
| Settings | Profile photo, email, password, linked accounts, GDPR export, account deletion |

---

## Auth

- **Google OAuth** and **GitHub OAuth** via ASP.NET Identity
- **Spotify OAuth** via custom `SpotifyAuthController`
- Full account management: photo, email, password, linked accounts, phone, GDPR export + deletion

---

## Email

Transactional email via [Resend](https://resend.com) on verified domain `statify.one`:
- Password reset — forgot password → email → reset link → confirmation
- Branded dark-theme HTML template with Statify logo

---

## Design System

- **Dark theme** — `#080808` base, `#111` cards, `#1DB954` Spotify green accent
- **No inline styles** — all styles in per-page CSS files
- **Responsive** — tablet (≤900px) and mobile (≤600px) breakpoints
- **Fonts** — Syne (headings), DM Sans (body)

---

## Local Setup

```bash
cd backend
dotnet restore
dotnet run
```

### Required Environment Variables

```
ConnectionStrings__DefaultConnection      # SQL Server (app DB)
ConnectionStrings__MusicHistoryConnection # SQL Server (music history)
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Authentication__GitHub__ClientId
Authentication__GitHub__ClientSecret
Spotify__ClientId
Spotify__ClientSecret
Spotify__RedirectUri
RESEND_API_KEY
JWT_SECRET
```

---

## Railway Deployment

1. Connect GitHub repo in Railway → Settings → Source
2. Set **Root Directory** to `backend`
3. Add all environment variables above in Railway → Variables
4. Deploy — Railway builds from `backend/` automatically on every `git push`
