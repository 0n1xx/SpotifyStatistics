# Statify — Spotify Statistics

A full-stack Spotify analytics platform. Statify ingests your listening history via Apache Airflow, enriches it with metadata from external APIs, stores it at scale across two databases, and surfaces it through a polished dark-themed web application.

---

## Architecture Overview

```
Spotify API
    │
    ▼
Apache Airflow (Python)
    ├── Fetch listening history (Spotify API)
    ├── Enrich with MusicBrainz (artist country, city)
    ├── Deduplicate & normalize
    ├── ClickHouse (analytics storage)
    └── Microsoft SQL Server (web backend storage)
              │
              ▼
    ASP.NET Core Web App (C#)
              ├── Google OAuth + GitHub OAuth
              ├── Spotify OAuth (custom controller)
              ├── Dashboard, Recently Played, World Map
              ├── Account management + GDPR export
              └── Transactional email via Resend (statify.one)
                        │
                        ▼
               iOS App (Planned)
```

---

## Project Structure

```
SpotifyStatistics/
├── DataComponent/              # Python / Airflow data pipeline
│   └── spotify_history.py      # Full DAG: fetch → enrich → load
├── Pages/                      # Razor Pages (web UI)
│   ├── Dashboard.cshtml
│   ├── RecentlyPlayed.cshtml
│   ├── Worldmap.cshtml
│   └── Settings.cshtml
├── Areas/Identity/             # ASP.NET Identity (auth + account management)
│   └── Pages/Account/
│       ├── Login / Register
│       ├── ForgotPassword / ResetPassword
│       └── Manage/ (Profile, Email, Password, Linked accounts, GDPR)
├── Controllers/                # SpotifyAuthController
├── Services/
│   └── ResendEmailSender.cs    # Transactional email via Resend API
├── Models/                     # Data models
├── Migrations/                 # EF Core migrations
└── wwwroot/                    # CSS, JS, static assets
    ├── css/                    # Per-page stylesheets (no inline styles)
    ├── js/                     # Per-page scripts
    └── statify_email_logo.png  # Brand logo for transactional emails
```

---

## Components

### DataComponent — Airflow Data Pipeline (Python)

An Apache Airflow DAG (`spotify_history`) that runs every 30 minutes per user.

| Task | Description |
|---|---|
| `get_users` | Fetches all users + refresh tokens from SQL Server |
| `fetch_user` | Pulls last 50 played tracks from Spotify API per user |
| `combine` | Flattens per-user results into a single list |
| `enrich` | Enriches each artist with country + city via MusicBrainz API |
| `load_clickhouse` | Deduplicates and inserts into ClickHouse |
| `fix_artist_country_conflicts` | Resolves conflicting country data using majority vote |
| `load_mssql` | Syncs new records from ClickHouse to SQL Server for the web app |

**Normalization applied at ingest:**
- Artist names → Title Case (merges encoding artifacts)
- Song / album → lowercase
- Timezone conversion UTC → Toronto before deriving date (prevents off-by-one day errors)
- Invalid MusicBrainz country codes (`XW`) → `unknown`
- Country-level `begin_area` values (e.g. "United States") → `unknown`

---

### Web Application (ASP.NET Core / C#)

A dark-themed web app deployed on Railway at [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app).

#### Authentication

- **Google OAuth** and **GitHub OAuth** via ASP.NET Identity
- **Spotify OAuth** for data access via custom `SpotifyAuthController`
- Full account management: profile photo, email, password, linked accounts, phone number, GDPR data export and account deletion

#### Pages

| Page | Description |
|---|---|
| Dashboard | Top tracks, artists, albums, listening by hour, activity by month |
| Recently Played | Paginated history with search and time range filter |
| World Map | D3.js geographic visualization of artists by country |
| Settings | Profile, connected accounts, danger zone |

#### Email

Transactional email powered by [Resend](https://resend.com) on the verified `statify.one` domain:
- Password reset flow (forgot password → email → reset link → confirmation)
- Branded dark-theme HTML email with Statify logo

---

## Tech Stack

| Layer | Technology |
|---|---|
| Data pipeline | Python, Apache Airflow |
| External APIs | Spotify API, MusicBrainz API |
| Analytics storage | ClickHouse |
| App database | Microsoft SQL Server |
| Web backend | ASP.NET Core (C#), Razor Pages |
| Auth | ASP.NET Identity, Google OAuth, GitHub OAuth, Spotify OAuth |
| Email | Resend API (`noreply@statify.one`) |
| Frontend | Vanilla JS, custom CSS design system |
| Fonts | Syne (headings), DM Sans (body) |
| Hosting | Railway |
| Domain | statify.one (Namecheap) |

---

## Design System

- **Dark theme** — `#080808` base, `#111` cards, `#1DB954` Spotify green accent
- **No inline styles** — all styles in per-page CSS files
- **Separate concerns** — CSS, JS, and HTML in separate files per page
- **Responsive** — tablet (≤900px) and mobile (≤600px) breakpoints
- **Accessible** — semantic HTML, ARIA labels, `role` attributes throughout

---

## Roadmap

- [x] Python / Airflow data pipeline (extraction, enrichment, deduplication, loading)
- [x] ASP.NET web app with Spotify OAuth
- [x] Dashboard, Recently Played, World Map pages
- [x] Account management (email, password, photo, linked accounts, GDPR export)
- [x] Forgot password + transactional email via Resend
- [x] Custom domain `statify.one` with DNS verification
- [x] Branded HTML email template
- [ ] iOS app (Swift) — listening insights and interactive analytics

---

## Getting Started

### DataComponent (Airflow)

```bash
cd DataComponent
pip install -r requirements.txt
```

Requires Airflow variables:
- `CLICKHOUSE_CONN` — ClickHouse connection string
- `MSSQL_CONN_SPOTIFY` — SQL Server connection (Spotify tokens)
- `MSSQL_CONN_MASTER` — SQL Server connection (music history)
- `VLAD_SPOTIFY_CLIENT_ID` / `VLAD_SPOTIFY_SECRET_KEY` — Spotify app credentials
- `SERVER_LOOPBACK` — Spotify OAuth redirect URI

### Web App

```bash
dotnet restore
dotnet run
```

Required environment variables (Railway → Variables):
- `ConnectionStrings__DefaultConnection` — SQL Server connection string
- `Authentication__Google__ClientId` / `ClientSecret`
- `Authentication__GitHub__ClientId` / `ClientSecret`
- `Spotify__ClientId` / `ClientSecret` / `RedirectUri`
- `RESEND_API_KEY` — Resend API key for transactional email

---

*Built to demonstrate skills in data engineering, backend development, and full-stack system design.*
