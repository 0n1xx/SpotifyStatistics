# Statify — iOS App (Swift)

> Native iOS companion to the Statify web platform. Full feature parity with the web app — built in Swift with SwiftUI.

**Status: In Development**

---

## Planned Features

| Feature | Web | iOS |
|---|---|---|
| Spotify OAuth login | ✅ | 🔲 |
| Google / GitHub OAuth | ✅ | 🔲 |
| Dashboard — top tracks, artists, albums | ✅ | 🔲 |
| Listening by hour chart | ✅ | 🔲 |
| Activity heatmap by month | ✅ | 🔲 |
| Recently Played — paginated history | ✅ | 🔲 |
| Search + time range filter | ✅ | 🔲 |
| World Map — artist origins | ✅ | 🔲 |
| Profile photo + display name | ✅ | 🔲 |
| Email + password management | ✅ | 🔲 |
| Linked accounts | ✅ | 🔲 |
| GDPR data export | ✅ | 🔲 |
| Account deletion | ✅ | 🔲 |

---

## Tech Stack

| | |
|---|---|
| **Language** | Swift 6 |
| **UI** | SwiftUI |
| **Charts** | Swift Charts |
| **Maps** | MapKit |
| **Networking** | URLSession + async/await |
| **Auth** | ASP.NET Identity via REST · Spotify OAuth (ASWebAuthenticationSession) |
| **State** | `@Observable` / `@StateObject` |
| **Min target** | iOS 17 |

---

## Architecture

```
Statify iOS
├── Auth
│   ├── LoginView
│   ├── RegisterView
│   ├── SpotifyConnectView      # Spotify OAuth via ASWebAuthenticationSession
│   └── AuthViewModel           # Token storage in Keychain
│
├── Dashboard
│   ├── DashboardView
│   ├── TopTracksSection        # Swift Charts bar chart
│   ├── TopArtistsSection
│   ├── TopAlbumsSection
│   ├── ListeningByHourChart    # Swift Charts
│   └── ActivityHeatmap         # Custom SwiftUI calendar grid
│
├── RecentlyPlayed
│   ├── RecentlyPlayedView      # Paginated list
│   ├── SearchBar
│   ├── TimeRangePicker         # 7d / 30d / 90d / All time
│   └── TrackRow
│
├── WorldMap
│   ├── WorldMapView            # MapKit + country polygons
│   ├── CountryAnnotation       # Artist count per country
│   └── CountryDetailSheet
│
├── Settings
│   ├── SettingsView
│   ├── ProfileSection          # Photo + display name
│   ├── AccountSection          # Email · password · phone
│   ├── LinkedAccountsSection   # Spotify · Google · GitHub
│   └── DangerZone              # GDPR export · account deletion
│
└── Core
    ├── APIClient               # URLSession wrapper
    ├── AuthTokenManager        # Keychain JWT storage
    ├── Models/                 # Codable data models
    └── Extensions/
```

---

## Backend Integration

The iOS app connects to the existing ASP.NET backend via REST API.

Endpoints to expose from `web/`:

```
POST   /api/auth/login
POST   /api/auth/register
GET    /api/auth/spotify/connect
GET    /api/dashboard
GET    /api/recently-played?page=&q=
GET    /api/worldmap
GET    /api/settings/profile
PUT    /api/settings/profile
PUT    /api/settings/email
PUT    /api/settings/password
GET    /api/settings/export
DELETE /api/account
```

---

## Build Plan

### Phase 1 — Foundation
- [ ] Project setup (SwiftUI, targets, folder structure)
- [ ] `APIClient` with async/await + JWT injection
- [ ] Keychain token storage
- [ ] Login + Register screens
- [ ] Spotify OAuth via `ASWebAuthenticationSession`

### Phase 2 — Core Screens
- [ ] Dashboard — top tracks, artists, albums
- [ ] Listening by hour (Swift Charts)
- [ ] Activity heatmap (custom SwiftUI grid)
- [ ] Recently Played — paginated + search + time filter

### Phase 3 — Map + Settings
- [ ] World Map — MapKit with country annotations
- [ ] Country detail sheet
- [ ] Settings — profile, email, password
- [ ] Linked accounts + GDPR export + account deletion

### Phase 4 — Polish
- [ ] Dark theme matching web design system
- [ ] Syne + DM Sans fonts
- [ ] Skeleton loading states
- [ ] Error handling + empty states
- [ ] App icon + launch screen
- [ ] TestFlight distribution

---

## Design System

Matches the Statify web design exactly:

| Token | Value |
|---|---|
| Background | `#080808` |
| Card | `#111111` |
| Accent | `#1DB954` |
| Text primary | `#FFFFFF` |
| Text secondary | `#999999` |
| Heading font | Syne |
| Body font | DM Sans |

---

## Getting Started

```bash
cd ios
open Statify.xcodeproj
```

Requires Xcode 16+ · iOS 17+
