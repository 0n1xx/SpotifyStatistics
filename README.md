<div align="center">

<img src="docs/logo.svg" alt="Statify" width="120" height="120" />

# Statify

**Personal Spotify analytics platform — built to track, enrich, and visualize your listening history at scale.**

[![Live](https://img.shields.io/badge/Live-statify-1DB954?style=flat-square&logo=railway&logoColor=white)](https://spotifystatistics-production.up.railway.app/)
[![Platform](https://img.shields.io/badge/Platform-Web%20%7C%20iOS-1DB954?style=flat-square)](#)

</div>

---

## Overview

Statify is an end-to-end analytics platform that continuously ingests your Spotify listening history, enriches each track with geographic artist metadata, and surfaces insights across a web application and a native iOS companion app.

The data pipeline runs every **30 minutes**, pulling the latest 50 played tracks per user via the Spotify API, resolving artist origins through MusicBrainz, and distributing records across two purpose-built databases — **PostgreSQL** for fast analytic queries (Superset) and **SQL Server** for the web application layer.

On top of that, **Ask Statify** is a floating chat assistant that answers questions about the *current* user's profile and listening stats (from SQL Server) and their Google Calendar schedule — without ever exposing another user's data.

---

## Architecture

```
Spotify API
    │
    ▼
Apache Airflow  ──────────────────────────  every 30 min
    ├── fetch_user      →  last 50 played tracks per user
    ├── enrich          →  artist country + city (MusicBrainz)
    ├── dedup           →  deduplication by played_at + user_id
    ├── load_postgres   →  PostgreSQL (analytics store)
    └── load_mssql      →  SQL Server (web app store)
                                    │
                                    ▼
                         ASP.NET Core Web App
                         ├── Dashboard / History / Map / Settings
                         ├── Ask Statify chat (OpenAI)
                         │     ├── UserProfiles + music_history (SQL Server)
                         │     └── Google Calendar API (read-only)
                         └── REST API for iOS
                                    │
                                    ▼
                            iOS App (Swift)
```

**Ask Statify flow (important):** ChatGPT does **not** connect to your database or Google directly. The ASP.NET backend authenticates the user, loads **only their** data, builds a text context, then sends that context + the question to OpenAI.

```
Browser chat widget
    → POST /api/chat (cookie auth)
    → ChatController (UserId from claims)
    → SQL Server (profile + listening stats)
    → Google Calendar (only if the question is schedule-related)
    → OpenAI Chat Completions
    → JSON { reply } → widget
```

---

## Repository Structure

```
Statify/
├── web/                 # ASP.NET Core (C#) — Razor Pages web application + chat API
├── data_component/      # Python — Apache Airflow DAG + custom Docker image
├── ios/                 # Swift — native iOS companion app
└── README.md
```

---

## Tech Stack

### Data & Infrastructure

| Layer | Technology |
|---|---|
| Orchestration | Apache Airflow (custom Docker image) |
| Analytics DB | PostgreSQL |
| App DB | Microsoft SQL Server |
| Airflow metadata | PostgreSQL |
| Analytics UI | Apache Superset (custom Docker image) |
| Hosting | Railway |

> **Note:** Both Apache Airflow and Apache Superset are deployed using **custom Docker images** — not the official defaults. Each image is purpose-built for Railway's environment with pre-configured connections, environment injection, and production-hardened settings.

### Web Application

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 · Razor Pages |
| Language | C# |
| Auth | ASP.NET Identity · Google OAuth · GitHub OAuth · Spotify OAuth |
| ORM | Entity Framework Core |
| Frontend | Vanilla JS · Custom CSS |
| Maps | D3.js |
| AI chat | OpenAI `gpt-4o-mini` via `/api/chat` |
| Calendar | Google Calendar API (read-only, per-user OAuth tokens) |
| Email | Resend API (`noreply@statify.one`) |

### iOS

| Layer | Technology |
|---|---|
| Language | Swift 6 |
| UI | SwiftUI |
| Charts | Swift Charts |
| Maps | MapKit |
| Networking | URLSession + async/await |
| Min target | iOS 17 |

---

## Features

### Web Application

| Page / Feature | Description |
|---|---|
| **Dashboard** | Top tracks, artists, albums · listening by hour · activity by month |
| **Recently Played** | Paginated full history with search |
| **World Map** | D3.js visualization — artist origins plotted by country |
| **Settings** | Profile · linked accounts · **display time zone** (client-only) · GDPR export |
| **Ask Statify** | Floating chat: profile + listening stats from SQL Server; schedule from Google Calendar |

### Ask Statify (chat)

| Capability | Details |
|---|---|
| Auth | Only logged-in users (`[Authorize]`); `UserId` always from identity claims |
| Spotify / profile | Answers using **this user's** `UserProfiles` + `music_history` only |
| Privacy | Refuses questions about other users — foreign rows never enter the prompt |
| Google Calendar | Read-only events for the Google account used to sign in; tokens stored in `GoogleCalendarTokens` |
| General knowledge | Can answer general music questions like ChatGPT when not user-specific |

### Admin Dashboard (Superset)

Internal analytics dashboard powered by **Apache Superset**, connected directly to PostgreSQL. Provides platform-wide visibility into listening activity with the following charts:

- **KPI cards** — total plays, unique artists, unique songs, countries
- **Daily activity line chart** — streams, albums, and artists over time
- **Top 10 artists / albums / songs** — ranked by play count
- **Artist word cloud** — frequency-weighted visualization
- **Raw history table** — full unfiltered event log

![Statify Admin Dashboard](docs/dashboard.jpg)

---

## Design System

Consistent visual identity across web and iOS:

| Token | Value |
|---|---|
| Background | `#080808` |
| Card | `#111111` |
| Accent | `#1DB954` (Spotify green) |
| Text primary | `#FFFFFF` |
| Text secondary | `#999999` |
| Heading font | Syne |
| Body font | DM Sans |

---

## Display time zones

Pipeline timestamps remain stable in the database. Users can pick a **display time zone** in Settings (web `localStorage` / iOS `UserDefaults`) so Recently Played times look correct for Toronto, Cairo, etc. **without rewriting stored rows**.

Separately, the Airflow DAG can convert ingest `played_at`/`date` with `HISTORY_TIMEZONE` and optional `HISTORY_TIMEZONE_BY_USER` (see [`data_component/README.md`](./data_component/README.md)).

---

## Roadmap

- [x] Airflow pipeline — fetch, enrich, deduplicate, load
- [x] ASP.NET Core web app with full Spotify OAuth
- [x] Dashboard · Recently Played · World Map
- [x] Full account management + GDPR export
- [x] Transactional email via Resend on `statify.one`
- [x] Custom domain with DNS + SSL
- [x] Admin analytics dashboard (Superset + PostgreSQL)
- [x] iOS app — Phase 1: Auth + Dashboard
- [x] iOS app — Phase 2: Recently Played + World Map
- [x] iOS app — Phase 3: Settings + Polish
- [x] Ask Statify chat — OpenAI + current-user DB context
- [x] Google Calendar read-only integration for schedule questions
- [x] Client display time zone preference (web + iOS)
- [ ] Chat function-calling / tools for on-demand DB + Calendar queries
- [ ] Calendar FreeBusy (“am I free at 15:00?”) precision answers

---

## Modules

- [`data_component/`](./data_component/README.md) — Airflow DAG, timezone variables, and deployment
- [`web/`](./web/README.md) — ASP.NET Core web app, Ask Statify, Google Calendar, Railway
- [`ios/`](./ios/README.md) — Swift iOS app, architecture, display time zone

---

*Built to demonstrate end-to-end skills across data engineering, backend development, and full-stack system design.*
