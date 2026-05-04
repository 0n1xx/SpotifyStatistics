# Statify — Spotify Statistics Platform

A full-stack Spotify analytics platform. Statify ingests your listening history via Apache Airflow, enriches it with metadata from external APIs, stores it at scale across two databases, and surfaces it through a polished dark-themed web application.

**Live:** [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app)

---

## Architecture

```
Spotify API
    │
    ▼
Apache Airflow (Python)              ← data_component/
    ├── Fetch listening history
    ├── Enrich via MusicBrainz API
    ├── Deduplicate & normalize
    ├── ClickHouse  (analytics)
    └── SQL Server  (web app)
              │
              ▼
    ASP.NET Core Web App (C#)        ← web/
              ├── Google + GitHub OAuth
              ├── Spotify OAuth
              ├── Dashboard, Recently Played, World Map
              └── Account management + GDPR export
                        │
                        ▼
               iOS App (Swift)       ← ios/  [planned]
```

---

## Repository Structure

```
Statify/
├── web/            # ASP.NET Core web application (C#)
├── data_component/     # Python / Apache Airflow data pipeline
├── ios/                # iOS app (Swift) — planned
└── README.md
```

See each folder's README for setup instructions.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Data pipeline | Python, Apache Airflow |
| External APIs | Spotify API, MusicBrainz API |
| Analytics DB | ClickHouse |
| App DB | Microsoft SQL Server |
| Web backend | ASP.NET Core (C#), Razor Pages |
| Auth | ASP.NET Identity, Google OAuth, GitHub OAuth, Spotify OAuth |
| Email | Resend API (`noreply@statify.one`) |
| Frontend | Vanilla JS, custom CSS |
| Fonts | Syne (headings), DM Sans (body) |
| Hosting | Railway |
| Domain | statify.one |

---

## Roadmap

- [x] Airflow data pipeline — fetch, enrich, deduplicate, load
- [x] ASP.NET web app with Spotify OAuth
- [x] Dashboard, Recently Played, World Map
- [x] Account management + GDPR export
- [x] Transactional email via Resend on `statify.one`
- [x] Custom domain with DNS verification
- [ ] iOS app (Swift) — listening insights and analytics

---

*Built to demonstrate skills in data engineering, backend development, and full-stack system design.*
