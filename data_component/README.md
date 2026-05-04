# Statify — Data Component (Apache Airflow)

Python-based data pipeline built on Apache Airflow. Fetches Spotify listening history, enriches it with geographic metadata, and loads it into ClickHouse and SQL Server.

---

## DAG: `spotify_history`

Runs every 3 minutes. Processes all registered users in parallel.

```
get_users → fetch_user[] → combine → enrich → load_clickhouse → fix_artist_country_conflicts → load_mssql
```

| Task | Description |
|---|---|
| `get_users` | Fetches all users + Spotify refresh tokens from SQL Server |
| `fetch_user` | Pulls last 50 played tracks per user from Spotify API |
| `combine` | Flattens per-user results into a single list |
| `enrich` | Enriches each artist with country + city via MusicBrainz API |
| `load_clickhouse` | Deduplicates and inserts new records into ClickHouse |
| `fix_artist_country_conflicts` | Resolves conflicting country data using majority vote |
| `load_mssql` | Syncs new ClickHouse records to SQL Server for the web app |

---

## Normalization

- Artist names → Title Case
- Song / album → lowercase
- Timezone UTC → Toronto before deriving `date` (prevents off-by-one day bugs)
- Invalid MusicBrainz country codes (`XW`) → `unknown`
- Country-level `begin_area` values → `unknown`

---

## Setup

```bash
cd data_component
pip install -r requirements.txt
```

## Airflow Variables Required

| Variable | Description |
|---|---|
| `CLICKHOUSE_CONN` | `clickhouse://user:pass@host:9000/default` |
| `MSSQL_CONN_SPOTIFY` | SQL Server — Spotify tokens |
| `MSSQL_CONN_MASTER` | SQL Server — music history |
| `VLAD_SPOTIFY_CLIENT_ID` | Spotify app Client ID |
| `VLAD_SPOTIFY_SECRET_KEY` | Spotify app Client Secret |
| `SERVER_LOOPBACK` | Spotify OAuth redirect URI |

## Railway Deployment

Push `spotify_history.py` to the `AirflowRailway` GitHub repo → Railway rebuilds Docker image automatically.
