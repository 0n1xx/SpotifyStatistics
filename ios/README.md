# Statify — iOS

> Native iOS companion to the Statify web platform. Full feature parity with core web analytics — built in Swift with SwiftUI.

**Status: Complete** (Ask Statify chat remains web-first)

---

## Demo

https://github.com/user-attachments/assets/dbe9da7e-1eba-482b-bacb-de784a5cd004
<!-- To add your screencast: drag the video file into any GitHub Issue comment box, wait for the upload link to appear, then paste it here in place of the URL above. -->

---

## Overview

The Statify iOS app connects to the existing ASP.NET Core backend via REST API, sharing the same authentication system, data layer, and business logic. The mobile experience is designed to match the Statify web design system exactly — dark theme, Syne headings, DM Sans body, and the `#1DB954` green accent.

**Display time zone** preference lives on-device (`UserDefaults`) and only changes how Recently Played timestamps are formatted — it does not rewrite database values.

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
| Preferences | UserDefaults (display time zone) |
| Minimum Target | iOS 17 |

---

## Architecture

```
Statify iOS
├── Core/
│   ├── Auth/
│   │   ├── AuthManager.swift          # Session lifecycle, token refresh
│   │   └── OAuthHelper.swift          # Spotify OAuth via ASWebAuthenticationSession
│   ├── Network/
│   │   ├── APIClient.swift            # URLSession wrapper with JWT injection
│   │   └── KeychainManager.swift      # Secure token storage
│   └── DisplayTimeZone.swift          # Client-only IANA TZ preference + formatters
│
├── DesignSystem/
│   ├── AppColors.swift
│   ├── AppFonts.swift
│   └── AppStyles.swift
│
└── Features/
    ├── Auth/
    │   ├── LoginView.swift
    │   └── RegisterView.swift
    │
    ├── Dashboard/
    │   ├── DashboardView.swift
    │   ├── DashboardViewModel.swift
    │   └── DashboardModels.swift
    │
    ├── RecentlyPlayed/
    │   ├── RecentlyPlayedView.swift
    │   ├── RecentlyPlayedViewModel.swift
    │   └── RecentlyPlayedModels.swift   # Formats playedAt via DisplayTimeZone
    │
    ├── WorldMap/
    │   ├── WorldMapView.swift
    │   ├── WorldMapViewModel.swift
    │   └── WorldMapModels.swift
    │
    ├── Settings/
    │   └── SettingsView.swift           # Profile + display time zone picker
    │
    └── MainTabView.swift
```

---

## Backend Integration

The iOS app connects to the ASP.NET Core backend. Endpoints consumed:

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

All authenticated requests attach a JWT Bearer token from Keychain. Token refresh is handled by `AuthManager`.

> **Note:** `POST /api/chat` (Ask Statify + Google Calendar) is currently used by the **web** widget. iOS can call the same endpoint later if/when a mobile chat UI is added.

---

## Features

| Feature | Web | iOS |
|---|---|---|
| Spotify OAuth login | ✅ | ✅ |
| Google / GitHub OAuth | ✅ | — |
| Dashboard — top tracks, artists, albums | ✅ | ✅ |
| Listening by hour chart | ✅ | ✅ |
| Activity by month | ✅ | ✅ |
| Recently Played — paginated history | ✅ | ✅ |
| Search + time range filter | ✅ | ✅ |
| World Map — artist origins | ✅ | ✅ |
| Profile photo + display name | ✅ | ✅ |
| Email + password management | ✅ | ✅ |
| Linked accounts | ✅ | ✅ |
| GDPR data export | ✅ | ✅ |
| Account deletion | ✅ | ✅ |
| Display time zone preference | ✅ | ✅ |
| Ask Statify chat + Google Calendar | ✅ | — |

---

## Display time zone

| Detail | Behavior |
|---|---|
| Where | Settings → **Display preferences → Time zone** |
| Storage | `UserDefaults` key `statify.displayTimeZone` |
| Effect | Recently Played formatting via `DisplayTimeZone.formatPlayedAt` |
| Pool | Curated IANA list (Toronto, Cairo, London, …) + device zone if missing |

Same idea as web: **display only** — DB / pipeline values stay unchanged.

If Xcode does not auto-include new files, ensure `Core/DisplayTimeZone.swift` is in the Statify app target.

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

Configure the backend base URL (see `AppConfig.swift` / `APIClient.swift`):

```swift
// production example
https://spotifystatistics-production.up.railway.app
```

Build and run on your simulator or device.

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
