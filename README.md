# Statify — Spotify Statistics

A full-stack Spotify analytics platform. Statify ingests your listening history, enriches it with metadata from external APIs, stores it at scale, and surfaces it through a polished web application — with a mobile app on the way.

---

## Architecture Overview

```
Spotify API
    │
    ▼
DataComponent (Python)
    ├── Enrichment via MusicBrainz API
    ├── Transformation & filtering
    ├── ClickHouse (analytics storage)
    └── Microsoft SQL Server (web backend storage)
              │
              ▼
    ASP.NET Web App (C#)
              ├── Spotify OAuth
              ├── Dashboard & stats
              ├── World map visualization
              └── Account management
                        │
                        ▼
               iOS App (Planned)
```

---

## Project Structure

```
SpotifyStatistics/
├── DataComponent/          # Python data pipeline
├── Pages/                  # Razor Pages (web UI)
│   ├── Dashboard.cshtml
│   ├── RecentlyPlayed.cshtml
│   ├── Worldmap.cshtml
│   └── Settings.cshtml
├── Areas/Identity/         # ASP.NET Identity (auth)
├── Controllers/            # SpotifyAuthController
├── Models/                 # Data models
├── wwwroot/                # CSS, JS, static assets
└── DataComponent/          # Python pipeline
```

---

## Components

### DataComponent — Data Pipeline (Python)

Handles the full ETL flow from Spotify to storage.

- **Extraction** — Pulls listening history from the Spotify API
- **Enrichment** — Fetches additional metadata (genres, artist info, release details) from MusicBrainz
- **Transformation** — Cleans, deduplicates, and normalizes records
- **Loading** — Writes to ClickHouse (analytics) and Microsoft SQL Server (web backend)

### Web Application (ASP.NET Core / C#)

A dark-themed web app that lets users explore their Spotify listening data.

**Authentication**
- Google OAuth and GitHub OAuth via ASP.NET Identity
- Spotify OAuth for data access (custom `SpotifyAuthController`)
- Full account management: email, password, linked accounts, data export

**Pages**
- **Dashboard** — Top tracks, artists, genres, listening trends
- **Recently Played** — Paginated history with track metadata
- **World Map** — Geographic visualization of artists by country
- **Settings** — Profile, connected accounts, account deletion

**Stack**
- ASP.NET Core Razor Pages
- Microsoft SQL Server (user data, Spotify tokens)
- Custom CSS design system (dark theme, `DM Sans` + `Syne` fonts)

---

## Tech Stack

| Layer | Technology |
|---|---|
| Data pipeline | Python, Spotify API, MusicBrainz API |
| Analytics storage | ClickHouse |
| App database | Microsoft SQL Server |
| Web backend | ASP.NET Core (C#) |
| Auth | ASP.NET Identity, Google OAuth, GitHub OAuth, Spotify OAuth |
| Frontend | Razor Pages, vanilla JS, custom CSS |
| Hosting | Railway |

---

## Roadmap

- [x] Python data pipeline (extraction, enrichment, loading)
- [x] ASP.NET web app with Spotify OAuth
- [x] Dashboard, Recently Played, World Map pages
- [x] Account management (email, password, linked accounts, GDPR export)
- [ ] iOS app (Swift) — listening insights and interactive analytics

---

## Getting Started

### DataComponent

```bash
cd DataComponent
pip install -r requirements.txt
python spotify_history.py
```

Requires a `.env` with `SPOTIFY_CLIENT_ID`, `SPOTIFY_CLIENT_SECRET`, and database connection strings.

### Web App

```bash
dotnet restore
dotnet run
```

Configure `appsettings.json` with your SQL Server connection string and OAuth credentials for Spotify, Google, and GitHub.

---

*Built to demonstrate skills in data engineering, backend development, and full-stack system design.*
