# Statify — Web

> Dark-themed web application built with ASP.NET Core and Razor Pages. Continuously synced with the Airflow data pipeline, deployed on Railway at [spotifystatistics-production.up.railway.app](https://spotifystatistics-production.up.railway.app/).

Includes **Ask Statify** (OpenAI chat over the current user's DB data), **Google Calendar** (read-only schedule answers), and a **Telegram bot** that sends a weekly listening summary.

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
| AI | OpenAI Chat Completions (`gpt-4o-mini`) |
| Calendar | Google Calendar API (read-only) |
| Email | Resend API (`noreply@statify.one`) |
| Telegram | Telegram Bot API (weekly summary + on-demand `/summary`) |
| Hosting | Railway |

---

## Project Structure

```
web/
├── Areas/Identity/             # ASP.NET Identity — auth + full account management
│   └── Pages/Account/
│       ├── Login.cshtml / Register.cshtml
│       ├── ExternalLogin.cshtml.cs   # Saves Google Calendar tokens under AspNetUsers.Id
│       └── Manage/
│
├── Controllers/
│   ├── ChatController.cs           # POST /api/chat — Ask Statify
│   ├── SpotifyAuthController.cs    # Spotify OAuth flow
│   ├── TelegramController.cs       # link/unlink/status + webhook for the weekly summary bot
│   ├── ApiController.cs            # REST endpoints for iOS app
│   └── AccountLinkController.cs
│
├── Data/
│   └── ApplicationDbContext.cs     # EF Core — Identity, SpotifyTokens, UserProfiles, GoogleCalendarTokens, TelegramLinks
│
├── Migrations/
│
├── Models/
│   ├── UserProfile.cs
│   ├── SpotifyToken.cs
│   ├── GoogleCalendarToken.cs
│   ├── TelegramLink.cs             # UserId ↔ Telegram chat_id + weekly-summary opt-in
│   └── ...
│
├── Pages/
│   ├── Dashboard.cshtml
│   ├── RecentlyPlayed.cshtml
│   ├── Worldmap.cshtml
│   ├── Settings.cshtml             # Includes client display time zone preference
│   └── Shared/
│       ├── _AppLayout.cshtml
│       └── Partials/_ChatWidget.cshtml
│
├── Services/
│   ├── OpenAIService.cs            # OpenAI API wrapper
│   ├── GoogleCalendarService.cs    # Calendar list + events + token refresh
│   ├── GoogleCalendarTokenStore.cs
│   ├── ResendEmailSender.cs
│   ├── TelegramService.cs          # Bot API sendMessage + weekly summary text builder
│   ├── TelegramWeeklySummaryService.cs  # BackgroundService — sends Mondays 09:00 UTC
│   └── JwtService.cs
│
├── wwwroot/
│   ├── css/
│   └── js/
│       ├── chat-widget.js
│       ├── timezone.js             # Display TZ preference (localStorage)
│       ├── recently-played.js
│       └── settings.js
│
└── Program.cs                      # DI, Google Calendar scopes, chat services
```

---

## Pages

| Page | Description |
|---|---|
| **Dashboard** | Top tracks, artists, albums · listening activity by hour · monthly activity |
| **Recently Played** | Paginated history with search; times follow **Settings → Display time zone** |
| **World Map** | D3.js visualization — artist origins by country |
| **Settings** | Profile · linked accounts · **display time zone** · GDPR export · account deletion |
| **Ask Statify** | Floating chat FAB on authenticated pages |

---

## Ask Statify chat

Floating “Ask Statify” widget on authenticated layouts (desktop + mobile web). Same backend powers the **iOS Ask tab**. Endpoint: **`POST /api/chat`**.

### How it works

1. Client sends `{ message }` — web with Identity cookie (`credentials: "same-origin"`), iOS with `Authorization: Bearer <JWT>`.
2. `ChatController` resolves **`UserId` from claims** (never from the chat text); schemes: `Bearer,Identity.Application`.
3. Loads **only this user's** `UserProfiles` + `music_history` aggregates.
4. If the question looks schedule-related (`meeting`, `today`, `schedule`, …), also calls `GoogleCalendarService`.
5. Builds a text **context** + rules, sends to OpenAI via `OpenAIService`.
6. Returns `{ reply }` to the client.

OpenAI **never** opens SQL Server or Google — it only sees the text context the server assembled.

### Privacy

- SQL always filters by `@uid` = current authenticated user.
- System prompt forbids inventing other users' data.
- Questions about other accounts are refused.

### Key files

| File | Role |
|---|---|
| `wwwroot/js/chat-widget.js` | UI → `/api/chat` |
| `Controllers/ChatController.cs` | Auth, DB context, calendar gate, OpenAI call |
| `Services/OpenAIService.cs` | Chat Completions request |
| `Pages/Shared/Partials/_ChatWidget.cshtml` | Markup |

---

## Google Calendar integration

### OAuth setup (Google Cloud)

1. Enable **Google Calendar API**.
2. OAuth consent / **Data Access**: add scope  
   `https://www.googleapis.com/auth/calendar.readonly`
3. **Clients** → Web client redirect URI:  
   `https://spotifystatistics-production.up.railway.app/signin-google`
4. If the app is in **Testing**, add your Google accounts as **Test users**.

### Runtime behavior

| Step | What happens |
|---|---|
| Google sign-in | `Program.cs` requests `calendar.readonly` + `access_type=offline` + consent prompt |
| After login | `ExternalLogin.cshtml.cs` saves `access_token` / `refresh_token` into `GoogleCalendarTokens` keyed by **AspNetUsers.Id** |
| Chat schedule question | `GoogleCalendarService` refreshes token if needed, lists calendars, fetches events (today → +7 days), returns a text summary for OpenAI |

Tokens follow the **Google account used to sign into Statify**. Shared calendars (e.g. “Vlad & Temi”) only appear if that Google account can see them.

---

## Telegram weekly summary

A bot (`TelegramController` + `TelegramService` + `TelegramWeeklySummaryService`) that links a Telegram chat to a Statify account and sends a weekly listening summary.

### Setup (BotFather)

1. Message **@BotFather** → `/newbot` → get a bot token.
2. Railway → Variables → `Telegram__BotToken` (token) and `Telegram__WebhookSecret` (any random string).
3. `appsettings.Production.json` → `Telegram:BotUsername` (the bot's `@username`, no `@`).
4. Register the webhook once per bot-token/URL change:
   ```bash
   curl -F "url=https://spotifystatistics-production.up.railway.app/api/telegram/webhook" \
        -F "secret_token=<Telegram__WebhookSecret value>" \
        "https://api.telegram.org/bot<BOT_TOKEN>/setWebhook"
   ```

### How it works

| Step | What happens |
|---|---|
| Connect | Settings (web or iOS) → `POST /api/telegram/link` → returns a `t.me/<bot>?start=<token>` deep link |
| Linking | User taps **Start** in Telegram → `POST /api/telegram/webhook` → `TelegramController` matches the one-time token to a `TelegramLink` row and stores `ChatId` |
| Scheduled send | `TelegramWeeklySummaryService` (hosted `BackgroundService`) checks hourly, sends every Monday 09:00 UTC to each linked, opted-in user |
| On-demand | `/summary` in the bot chat triggers the same summary immediately |
| Opt out | `/stop` in the bot chat, or Disconnect in Settings |

### Key files

| File | Role |
|---|---|
| `Controllers/TelegramController.cs` | `/link`, `/unlink`, `/status`, `/weekly-summary`, `/webhook` |
| `Services/TelegramService.cs` | `sendMessage` wrapper + `BuildWeeklySummaryAsync` (shared by scheduled + on-demand) |
| `Services/TelegramWeeklySummaryService.cs` | Hourly check, sends on Mondays, gated by `TelegramLinks.LastSummarySentAtUtc` |
| `Models/TelegramLink.cs` | `UserId` ↔ `ChatId`, `LinkToken` (handshake), `WeeklySummaryEnabled` |

---

## Display time zone (client-only)

Database timestamps are **not** rewritten for display preference.

| Platform piece | Behavior |
|---|---|
| Settings → Time zone | Curated pool (Toronto, Cairo, London, …) |
| Web storage | `localStorage` key `statify.displayTimeZone` (`timezone.js`) |
| Recently Played | Formats `playedAt` with the selected IANA zone |

This lets friends in Egypt see local clock times without changing stored Toronto-pipeline rows.

---

## Authentication

| Provider | Implementation |
|---|---|
| Email / Password | ASP.NET Identity |
| Google OAuth | `Microsoft.AspNetCore.Authentication.Google` (+ Calendar scopes for chat) |
| GitHub OAuth | `AspNet.Security.OAuth.GitHub` |
| Spotify OAuth | Custom `SpotifyAuthController` |

OAuth providers link to the same ASP.NET Identity account. Connect / disconnect from Settings.

---

## Email

Transactional email via **Resend** on `statify.one`:

- Password reset flow
- Sender: `noreply@statify.one`

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

No inline styles — CSS/JS/HTML kept in separate files under `wwwroot/` and Razor partials.

---

## Local Setup

### Prerequisites

- .NET 10 SDK
- Microsoft SQL Server (local instance or remote)
- Spotify Developer App ([developer.spotify.com](https://developer.spotify.com))
- Google OAuth credentials + Calendar API ([console.cloud.google.com](https://console.cloud.google.com))
- GitHub OAuth App ([github.com/settings/applications](https://github.com/settings/applications))
- OpenAI API key ([platform.openai.com](https://platform.openai.com))
- Resend account + verified domain ([resend.com](https://resend.com))

### Install & Run

```bash
cd web
dotnet restore
dotnet run
```

The app will be available at `https://localhost:5001` (or the port shown by `dotnet run`).

### Environment Variables

Set these in `appsettings.Development.json` or as environment variables:

```
ConnectionStrings__DefaultConnection       # SQL Server — Identity, profiles, tokens, music history
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Authentication__GitHub__ClientId
Authentication__GitHub__ClientSecret
Spotify__ClientId
Spotify__ClientSecret
Spotify__RedirectUri                       # e.g. https://localhost:5001/callback
OpenAI__ApiKey                             # Ask Statify chat
RESEND_API_KEY
JWT_SECRET                                 # iOS API JWT tokens
Telegram__BotToken                         # Weekly summary bot (from @BotFather)
Telegram__WebhookSecret                    # Verifies incoming /api/telegram/webhook calls
```

`Telegram:BotUsername` is not a secret — set it in `appsettings.Production.json` instead.

Railway typically uses the same keys with `__` nesting (e.g. `OpenAI__ApiKey`).

### Database Migrations

```bash
cd web
dotnet ef database update
```

On startup the app also ensures helper tables/columns exist when needed (e.g. `GoogleCalendarTokens`, `UserProfiles.DisplayName`).

---

## Railway Deployment

1. Connect the GitHub repository in Railway → **Settings → Source**
2. Set the **Root Directory** to `web`
3. Add all environment variables listed above under Railway → **Variables**
4. Deploy — Railway builds and runs the app on every `git push` to `main`

After deploying Calendar changes, sign out and **Sign in with Google** again so calendar consent/tokens refresh.

After deploying Telegram changes (new bot token or URL), re-run the `setWebhook` curl command from [Telegram weekly summary](#telegram-weekly-summary) once.

---

## Quick chat test checklist

- `What is my display name?`
- `Who is my favourite artist?` / `What are my top albums?`
- `What do I have scheduled today?` (requires Google Calendar connected)
- `What is freezymlg listening to?` → should refuse
