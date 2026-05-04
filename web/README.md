# Statify — web (ASP.NET Core)

Dark-themed web application built with ASP.NET Core and Razor Pages. Deployed on Railway at [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app).

---

## Structure

```
web/
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
cd # Statify

> Personal Spotify analytics platform — built to track, enrich, and visualize your listening history at scale.

**Live → [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app)**

---

## What it does

Statify automatically pulls your Spotify listening history every 3 minutes, enriches each track with geographic artist data via MusicBrainz, and stores everything across two databases — ClickHouse for fast analytics queries, and SQL Server for the web app. The result is a personal dashboard showing your top tracks, artists, albums, listening patterns by hour and month, and a world map of where your artists come from.

---

## Architecture

```
Spotify API
    │
    ▼
Apache Airflow                  every 3 min
    ├── fetch    →  last 50 played tracks per user
    ├── enrich   →  artist country + city (MusicBrainz)
    ├── dedup    →  deduplication by played_at + user_id
    ├── load     →  ClickHouse  (analytics)
    └── sync     →  SQL Server  (web app)
                         │
                         ▼
              ASP.NET Core Web App
                         │
                         ▼
                  iOS App  [planned]
```

---

## Repository

```
Statify/
├── web/                # ASP.NET Core (C#) — Razor Pages web application
├── data_component/     # Python — Apache Airflow DAG
├── ios/                # Swift — iOS app (planned)
└── README.md
```

---

## Pages

| Page | Description |
|---|---|
| **Dashboard** | Top tracks, artists, albums · listening by hour · activity heatmap by month |
| **Recently Played** | Paginated full history with search and time range filter |
| **World Map** | D3.js visualization — artist origins mapped by country |
| **Settings** | Profile photo · email · password · linked accounts · GDPR data export |

---

## Tech Stack

| | |
|---|---|
| **Data pipeline** | Python · Apache Airflow · Spotify API · MusicBrainz API |
| **Storage** | ClickHouse (analytics) · Microsoft SQL Server (app) · PostgreSQL (Airflow metadata) |
| **Web** | ASP.NET Core · Razor Pages · Vanilla JS · Custom CSS |
| **Auth** | ASP.NET Identity · Google OAuth · GitHub OAuth · Spotify OAuth |
| **Email** | Resend API · `noreply@statify.one` |
| **Hosting** | Railway |
| **Domain** | statify.one |

---

## Data Pipeline

The Airflow DAG (`spotify_history`) runs every 3 minutes and processes all users concurrently:

```
get_users → fetch_user[] → combine → enrich → load_clickhouse → fix_conflicts → load_mssql
```

**Normalization at ingest:**
- Artist names → Title Case (resolves encoding inconsistencies)
- Song / album → lowercase
- Timezone UTC → Toronto conversion before deriving `date` (prevents off-by-one bugs)
- Invalid MusicBrainz codes (`XW`) and country-level `begin_area` → `unknown`

---

## Design

- Dark theme — `#080808` base · `#111` cards · `#1DB954` Spotify green accent
- No inline styles — all CSS in per-page files
- Fonts — Syne (headings) · DM Sans (body)
- Responsive — 900px (tablet) · 600px (mobile) breakpoints

---

## Roadmap

- [x] Airflow pipeline — fetch · enrich · deduplicate · load
- [x] ASP.NET web app with full Spotify OAuth
- [x] Dashboard · Recently Played · World Map
- [x] Full account management + GDPR export
- [x] Transactional email via Resend on `statify.one`
- [x] Custom domain with DNS + SSL
- [ ] iOS app (Swift)

---

*Built to demonstrate end-to-end skills across data engineering, web development, and full-stack system design.*# Statify

> Personal Spotify analytics platform — built to track, enrich, and visualize your listening history at scale.

**Live → [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app)**

---

## What it does

Statify automatically pulls your Spotify listening history every 3 minutes, enriches each track with geographic artist data via MusicBrainz, and stores everything across two databases — ClickHouse for fast analytics queries, and SQL Server for the web app. The result is a personal dashboard showing your top tracks, artists, albums, listening patterns by hour and month, and a world map of where your artists come from.

---

## Architecture

```
Spotify API
    │
    ▼
Apache Airflow                  every 3 min
    ├── fetch    →  last 50 played tracks per user
    ├── enrich   →  artist country + city (MusicBrainz)
    ├── dedup    →  deduplication by played_at + user_id
    ├── load     →  ClickHouse  (analytics)
    └── sync     →  SQL Server  (web app)
                         │
                         ▼
              ASP.NET Core Web App
                         │
                         ▼
                  iOS App  [planned]
```

---

## Repository

```
Statify/
├── web/                # ASP.NET Core (C#) — Razor Pages web application
├── data_component/     # Python — Apache Airflow DAG
├── ios/                # Swift — iOS app (planned)
└── README.md
```

---

## Pages

| Page | Description |
|---|---|
| **Dashboard** | Top tracks, artists, albums · listening by hour · activity heatmap by month |
| **Recently Played** | Paginated full history with search and time range filter |
| **World Map** | D3.js visualization — artist origins mapped by country |
| **Settings** | Profile photo · email · password · linked accounts · GDPR data export |

---

## Tech Stack

| | |
|---|---|
| **Data pipeline** | Python · Apache Airflow · Spotify API · MusicBrainz API |
| **Storage** | ClickHouse (analytics) · Microsoft SQL Server (app) · PostgreSQL (Airflow metadata) |
| **Web** | ASP.NET Core · Razor Pages · Vanilla JS · Custom CSS |
| **Auth** | ASP.NET Identity · Google OAuth · GitHub OAuth · Spotify OAuth |
| **Email** | Resend API · `noreply@statify.one` |
| **Hosting** | Railway |
| **Domain** | statify.one |

---

## Data Pipeline

The Airflow DAG (`spotify_history`) runs every 3 minutes and processes all users concurrently:

```
get_users → fetch_user[] → combine → enrich → load_clickhouse → fix_conflicts → load_mssql
```

**Normalization at ingest:**
- Artist names → Title Case (resolves encoding inconsistencies)
- Song / album → lowercase
- Timezone UTC → Toronto conversion before deriving `date` (prevents off-by-one bugs)
- Invalid MusicBrainz codes (`XW`) and country-level `begin_area` → `unknown`

---

## Design

- Dark theme — `#080808` base · `#111` cards · `#1DB954` Spotify green accent
- No inline styles — all CSS in per-page files
- Fonts — Syne (headings) · DM Sans (body)
- Responsive — 900px (tablet) · 600px (mobile) breakpoints

---

## Roadmap

- [x] Airflow pipeline — fetch · enrich · deduplicate · load
- [x] ASP.NET web app with full Spotify OAuth
- [x] Dashboard · Recently Played · World Map
- [x] Full account management + GDPR export
- [x] Transactional email via Resend on `statify.one`
- [x] Custom domain with DNS + SSL
- [ ] iOS app (Swift)

---

*Built to demonstrate end-to-end skills across data engineering, web development, and full-stack system design.*
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
2. Set **Root Directory** to `web`
3. Add all environment variables above in Railway → Variables
4. Deploy — Railway builds from `web/` automatically on every `git push`
