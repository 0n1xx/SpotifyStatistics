# Statify — Data Component

> Python-based data pipeline built on Apache Airflow. Fetches Spotify listening history every 3 minutes, enriches each track with geographic artist metadata via MusicBrainz, and distributes records across ClickHouse and SQL Server.

---

## Admin Dashboard

The data pipeline feeds directly into an **Apache Superset** analytics dashboard, giving platform-wide visibility into listening activity across all users.

![Statify Admin Dashboard](../docs/dashboard.png)

**Dashboard includes:**
- KPI cards — total plays, unique artists, unique songs, countries covered
- Daily activity chart — streams, albums, and artists over time
- Top 10 artists, albums, and songs by play count
- Artist word cloud (frequency-weighted)
- Raw listening history table

> Both Airflow and Superset run inside **custom Docker images** — not the official defaults. Each image is purpose-built for Railway's environment with pre-baked connections, environment variable injection, and production-hardened configuration.

---

## DAG: `spotify_history`

Runs every **3 minutes**. Processes all registered users in parallel via dynamic task mapping.

```
get_users → fetch_user[] → combine → enrich → load_clickhouse → fix_artist_country_conflicts → load_mssql
```

| Task | Description |
|---|---|
| `get_users` | Fetches all active users and their Spotify refresh tokens from SQL Server |
| `fetch_user` | Pulls the last 50 played tracks per user from the Spotify API |
| `combine` | Flattens per-user results into a single unified list |
| `enrich` | Resolves artist country and city via MusicBrainz API |
| `load_clickhouse` | Deduplicates by `played_at + user_id` and inserts new records into ClickHouse |
| `fix_artist_country_conflicts` | Resolves conflicting country data for the same artist using majority vote |
| `load_mssql` | Syncs new ClickHouse records to SQL Server for the web application layer |

---

## Data Normalization

All records are normalized at ingest before hitting either database:

- **Artist names** → Title Case (resolves API encoding inconsistencies)
- **Song / album titles** → lowercase
- **Timestamps** → UTC converted to Toronto timezone before deriving `date` (prevents off-by-one day bugs at midnight)
- **Invalid MusicBrainz country codes** (`XW`) → `unknown`
- **Country-level `begin_area` values** (not city-level) → `unknown`

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
clickhouse-driver==0.2.9
spotipy==2.25.1
```

---

## Local Setup

### Prerequisites

- Python 3.10+
- A running Apache Airflow instance (local or remote)
- Access to ClickHouse, SQL Server, and PostgreSQL (Airflow metadata DB)

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

The DAG reads all credentials from **Airflow Variables** (not environment variables). Set these in the Airflow UI under **Admin → Variables**, or via the CLI:

```bash
airflow variables set CLICKHOUSE_CONN "clickhouse://user:pass@host:9000/default"
```

| Variable | Description |
|---|---|
| `CLICKHOUSE_CONN` | ClickHouse connection string — `clickhouse://user:pass@host:9000/default` |
| `MSSQL_CONN_SPOTIFY` | SQL Server connection — Spotify OAuth tokens |
| `MSSQL_CONN_MASTER` | SQL Server connection — music history |
| `VLAD_SPOTIFY_CLIENT_ID` | Spotify Developer App — Client ID |
| `VLAD_SPOTIFY_SECRET_KEY` | Spotify Developer App — Client Secret |
| `SERVER_LOOPBACK` | Spotify OAuth redirect URI (must match app settings) |

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

- ClickHouse SQLAlchemy driver (`clickhouse-sqlalchemy`) installed at build time
- Default admin credentials configured via environment variables
- ClickHouse database connection pre-registered in `superset_config.py`
- PostgreSQL used as the Superset metadata database

To connect Superset to ClickHouse manually:

1. Navigate to **Data → Databases → + Database**
2. Select **ClickHouse** as the database type
3. Enter the connection URI: `clickhouse+native://user:pass@host:9000/default`
4. Test connection and save
