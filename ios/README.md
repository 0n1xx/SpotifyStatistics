# Statify — iOS

> Native iOS companion to the Statify web platform. Full feature parity with the web application — built in Swift with SwiftUI.

**Status: In Development**

---

## Overview

The Statify iOS app connects to the existing ASP.NET Core backend via REST API, sharing the same authentication system, data layer, and business logic. The mobile experience is designed to match the Statify web design system exactly — dark theme, Syne headings, DM Sans body, and the `#1DB954` green accent.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | Swift 6 |
| UI Framework | SwiftUI |
| Charts | Swift Charts |
| Maps | MapKit |
| Networking | URLSession + async/await |
| Auth | ASP.NET Identity via REST · Spotify OAuth (`ASWebAuthenticationSession`) |
| State Management | `@Observable` / `@StateObject` |
| Token Storage | Keychain |
| Minimum Target | iOS 17 |

---

## Architecture

```
Statify iOS
├── Core/
│   ├── Auth/
│   │   ├── AuthManager.swift          # Session lifecycle, token refresh
│   │   └── OAuthHelper.swift          # Spotify OAuth via ASWebAuthenticationSession
│   └── Network/
│       ├── APIClient.swift            # URLSession wrapper with JWT injection
│       └── KeychainManager.swift      # Secure token storage
│
├── DesignSystem/
│   ├── AppColors.swift                # Color tokens matching web design system
│   ├── AppFonts.swift                 # Syne + DM Sans font registration
│   └── AppStyles.swift                # Shared view modifiers and component styles
│
└── Features/
    ├── Auth/
    │   ├── LoginView.swift
    │   └── RegisterView.swift
    │
    ├── Dashboard/
    │   ├── DashboardView.swift        # Top tracks, artists, albums
    │   ├── DashboardViewModel.swift
    │   └── DashboardModels.swift
    │
    ├── RecentlyPlayed/
    │   ├── RecentlyPlayedView.swift   # Paginated history + search + time filter
    │   ├── RecentlyPlayedViewModel.swift
    │   └── RecentlyPlayedModels.swift
    │
    ├── WorldMap/
    │   ├── WorldMapView.swift         # MapKit + country polygons
    │   ├── WorldMapViewModel.swift
    │   └── WorldMapModels.swift
    │
    ├── Settings/
    │   └── SettingsView.swift         # Profile, email, password, linked accounts
    │
    └── MainTabView.swift              # Root tab navigation
```

---

## Backend Integration

The iOS app connects to the existing ASP.NET Core backend. The following REST endpoints are consumed:

```
POST   /api/auth/login
POST   /api/auth/register
GET    /api/auth/spotify/connect
GET    /api/dashboard
GET    /api/recently-played?page=&q=&range=
GET    /api/worldmap
GET    /api/settings/profile
PUT    /api/settings/profile
PUT    /api/settings/email
PUT    /api/settings/password
GET    /api/settings/export
DELETE /api/account
```

All authenticated requests attach a JWT Bearer token from Keychain. Token refresh is handled transparently by `AuthManager`.

---

## Features

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

## Build Plan

### Phase 1 — Foundation
- [ ] Project setup (SwiftUI, targets, folder structure)
- [ ] `APIClient` with async/await + JWT injection
- [ ] Keychain token storage via `KeychainManager`
- [ ] Login and Register screens
- [ ] Spotify OAuth via `ASWebAuthenticationSession`

### Phase 2 — Core Screens
- [ ] Dashboard — top tracks, artists, albums
- [ ] Listening by hour (Swift Charts bar chart)
- [ ] Activity heatmap (custom SwiftUI calendar grid)
- [ ] Recently Played — paginated list + search + time range filter

### Phase 3 — Map & Settings
- [ ] World Map — MapKit with country annotations and detail sheet
- [ ] Settings — profile photo, email, password management
- [ ] Linked accounts, GDPR export, account deletion

### Phase 4 — Polish
- [ ] Dark theme aligned with web design system
- [ ] Syne + DM Sans custom fonts
- [ ] Skeleton loading states
- [ ] Error handling and empty states
- [ ] App icon and launch screen
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

### Requirements

- Xcode 16+
- iOS 17+ simulator or physical device
- Access to the Statify backend (local or production)

### Setup

```bash
cd ios
open StatifyiOS/Statify.xcodeproj
```

Configure the backend base URL in `APIClient.swift`:

```swift
private let baseURL = "https://spotifystatistics-production.up.railway.app"
// or for local development:
// private let baseURL = "http://localhost:5000"
```

Build and run on your simulator or device.
