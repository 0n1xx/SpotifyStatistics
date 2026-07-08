# Statify — Data Component

> Python-based data pipeline built on Apache Airflow. Fetches Spotify listening history every 30 minutes, enriches each track with geographic artist metadata via MusicBrainz, and distributes records across PostgreSQL and SQL Server.

---

## Admin Dashboard

The data pipeline feeds directly into an **Apache Superset** analytics dashboard, giving platform-wide visibility into listening activity across all users.

![Statify Admin Dashboard](../docs/dashboard.jpg)

**Dashboard includes:**
- KPI cards — total plays, unique artists, unique songs, countries covered
- Daily activity chart — streams, albums, and artists over time
- Top 10 artists, albums, and songs by play count
- Artist word cloud (frequency-weighted)
- Raw listening history table

> Both Airflow and Superset run inside **custom Docker images** — not the official defaults. Each image is purpose-built for Railway's environment with pre-baked connections, environment variable injection, and production-hardened configuration.

---

## DAG: `spotify_history`

Runs every **30 minutes**. Processes all registered users in parallel via dynamic task mapping.

```
get_users → fetch_user[] → combine → enrich → load_postgres → fix_artist_country_conflicts → load_mssql
```

| Task | Description |
|---|---|
| `get_users` | Fetches all active users and their Spotify refresh tokens from SQL Server |
| `fetch_user` | Pulls the last 50 played tracks per user from the Spotify API (UTC `played_at`) |
| `combine` | Flattens per-user results into a single unified list |
| `enrich` | Resolves artist country and city via MusicBrainz API |
| `load_postgres` | Converts timestamps to user-local TZ, derives `date`, dedupes, inserts into PostgreSQL |
| `fix_artist_country_conflicts` | Resolves conflicting country data for the same artist using majority vote |
| `load_mssql` | Appends new Postgres records to SQL Server for the web app (deduped — never truncates) |

The web/iOS apps and **Ask Statify** chat read listening history from **SQL Server** (`music_history`).

---

## Deduplication

Spotify's **recently played** API returns the **last 50 tracks** on every DAG run (every 30 minutes). If you listen to 50 tracks in an hour, the same plays appear in both runs — that overlap is expected.

A play is considered a **duplicate** when all of these match:

`played_at` (to the second) + `song` + `artist` + `album` + `user_id`

| Stage | What happens |
|---|---|
| `combine` | Drops duplicates within the current batch |
| `load_postgres` | Dedup again + `ON CONFLICT … DO NOTHING` (needs unique index on Postgres) |
| `load_mssql` | Inserts only rows not already in MSSQL (`WHERE NOT EXISTS`) — old data is never deleted |

Run `scripts/add_music_history_dedup.sql` on Postgres and MSSQL once to add the unique index and remove any existing duplicate rows.

---

## Normalization & timezones

All records are normalized at ingest before hitting either database:

- **Artist names** → Title Case (resolves API encoding inconsistencies)
- **Song / album titles** → lowercase
- **Timestamps** → Spotify returns UTC; before deriving `date`, each row is converted to that user's local timezone
- **Invalid MusicBrainz country codes** (`XW`) → `unknown`
- **Country-level `begin_area` values** (not city-level) → `unknown`

### Why timezone matters

Hard-coding `America/Toronto` for every user breaks friends in other regions (e.g. Egypt / `Africa/Cairo`): late-night plays can land on the wrong calendar **day**, which confuses day-level analytics and can interact badly with dedup if keys ever include date.

### Airflow Variables for TZ

| Variable | Description |
|---|---|
| `HISTORY_TIMEZONE` | Default IANA zone for users without an override (default `America/Toronto`) |
| `HISTORY_TIMEZONE_BY_USER` | Optional JSON map `{ "aspNetUserId": "Africa/Cairo", ... }` |

Example `HISTORY_TIMEZONE_BY_USER`:

```json
{
  "9659c63c-9d61-4000-b42f-d2d85bb84275": "America/Toronto",
  "ef9f4d4d-fa7d-4f03-896d-147c4d98ae88": "Africa/Cairo"
}
```

User IDs come from SQL Server `UserProfiles` / `AspNetUsers`.

### Display vs ingest

- **Ingest (this DAG):** writes `played_at` / `date` using the rules above.
- **Display (web/iOS Settings):** client-only preference converts *how timestamps look* without rewriting DB rows. See [`web/README.md`](../web/README.md) and [`ios/README.md`](../ios/README.md).

---

## Project Structure

```
data_component/
├── spotify_history.py     # Airflow DAG definition and all task logic
└── requirements.txt       # Python dependencies
```

---

## Dependencies

```
pandas==2.2.2
psycopg2-binary==2.9.9
spotipy==2.25.1
```

---

## Local Setup

### Prerequisites

- Python 3.10+
- A running Apache Airflow instance (local or remote)
- Access to PostgreSQL (analytics + Airflow metadata), and SQL Server

### Install

```bash
cd data_component
pip install -r requirements.txt
```

### Place the DAG

Copy `spotify_history.py` into your Airflow `dags/` folder:

```bash
cp spotify_history.py $AIRFLOW_HOME/dags/
```

Airflow will auto-discover the DAG on the next scheduler heartbeat.

---

## Airflow Variables

The DAG reads credentials from **Airflow Variables** (Admin → Variables):

| Variable | Description |
|---|---|
| `PG_CONN` | PostgreSQL connection — music history (Superset analytics) |
| `MSSQL_CONN` | SQL Server — `SpotifyTokens`, `music_history`, and related app tables |
| `VLAD_SPOTIFY_CLIENT_ID` | Spotify Developer App — Client ID |
| `VLAD_SPOTIFY_SECRET_KEY` | Spotify Developer App — Client Secret |
| `SERVER_LOOPBACK` | Spotify OAuth redirect URI (must match app settings) |
| `HISTORY_TIMEZONE` | Default IANA timezone for `played_at`/`date` |
| `HISTORY_TIMEZONE_BY_USER` | Optional JSON map of AspNet `UserId` → IANA TZ |

`MSSQL_CONN` example (databaseasp.net — URL-encode `#` → `%23`, `!` → `%21`, `@` → `%40`):

```
mssql+pyodbc://db44161:PASSWORD@db44161.public.databaseasp.net:1433/db44161?driver=ODBC+Driver+18+for+SQL+Server&Encrypt=yes&TrustServerCertificate=yes
```

---

## Docker & Deployment

### Custom Airflow Image

The production Airflow instance runs on Railway using a **custom Docker image** — not the official `apache/airflow` base. The image is purpose-built for this project:

- Python dependencies from `requirements.txt` are pre-installed at build time
- Database connection strings and Airflow variables are injected via Railway environment variables at runtime
- The image is configured to run in `LocalExecutor` mode with PostgreSQL as the metadata store
- Health checks and Railway-specific startup commands are included in the Dockerfile

### Deployment Flow

```bash
git push origin main
# Railway detects changes in AirflowRailway repo
# Docker image is rebuilt automatically
# New container is deployed with zero-downtime restart
```

> The DAG file (`spotify_history.py`) is baked into the Docker image at build time. Any DAG changes require a new image build and redeploy.

---

## Apache Superset

The admin analytics dashboard runs on a separate **custom Superset Docker image**, also deployed on Railway.

Key customizations in the Superset image:

- Default admin credentials configured via environment variables
- PostgreSQL database connection pre-registered in `superset_config.py` (analytics + metadata)
