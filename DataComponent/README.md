# Spotify Listening History Analytics Pipeline

## Project Overview

This project implements an automated ETL pipeline for collecting, storing, and analyzing personal Spotify listening history. Data is extracted from the Spotify Web API, transformed, loaded into an analytical database, and visualized through interactive dashboards.

The solution demonstrates a production-like data workflow suitable for music consumption insights, including top artists, albums, tracks, and temporal listening patterns.

## A possible result:
![Spotify Stats Dashboard](spotify_stats_dashboard.jpg)

## Architecture

- **Apache Airflow** – Orchestrates the end-to-end ETL process (extraction, transformation, loading)
- **Spotipy** – Python client for Spotify Web API integration
- **ClickHouse** – Columnar database optimized for high-performance analytical queries on large datasets
- **Apache Superset** – BI tool for building dashboards and exploring stored data
- **Docker & Docker Compose** – Containerization and service orchestration for reproducible deployment

## Data Collected

For each played track:
- Playback timestamp
- Track name
- Artist(s)
- Album
- Duration and other metadata

## Key Features & Dashboards

Once loaded into ClickHouse and connected to Superset:
- Timeline of recently played tracks
- Rankings of top artists, albums, and songs (all-time or filtered by period)
- Daily/weekly/monthly listening trends
- Custom explorations via SQL queries in Superset

## Prerequisites

- Docker and Docker Compose
- Spotify Developer Account and registered application [](https://developer.spotify.com/dashboard/)

## Spotify API Setup

The project uses OAuth 2.0 with refresh token flow for secure, long-term access.

1. **Create a Spotify App**
   - Log in to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard/)
   - Create a new app
   - Note **Client ID** and **Client Secret**
   - Add a Redirect URI (e.g., `http://localhost:7777/callback`)

2. **Generate Refresh Token (One-Time)**
   - Run the provided `local_script.ipynb` notebook
   - Follow the authentication flow in your browser
   - Grant required permissions (`user-read-recently-played`, `user-library-read`, etc.)
   - Extract the refresh token from the output

3. **Store Credentials** in your Variables in Airflow:
- SPOTIFY_CLIENT_ID=your_client_id
- SPOTIFY_CLIENT_SECRET=your_client_secret
- SPOTIFY_REDIRECT_URI=http://localhost:7777/callback
- SPOTIFY_REFRESH_TOKEN=your_refresh_token
  
## Deployment Instructions

### 0) Prerequisites (Server)

- Ubuntu 24.04+ recommended.
```bash
sudo apt update
sudo apt install -y ca-certificates curl gnupg
# Docker install (official method)
curl -fsSL https://get.docker.com | sudo bash
sudo usermod -aG docker $USER
newgrp docker

docker --version
docker compose version
```
### 1) Create project folders:
```bash
sudo mkdir -p /opt/{airflow,clickhouse,superset}
sudo chown -R $USER:$USER /opt/{airflow,clickhouse,superset}
```
### 2) Create shared Docker network:
```bash
docker network create airflow_net || true
docker network ls | grep airflow_net
```
### 3) ClickHouse docker-compose.yml:
Create /opt/clickhouse/docker-compose.yml:
```bash
cat > /opt/clickhouse/docker-compose.yml <<'YAML'
services:
  clickhouse:
    image: clickhouse/clickhouse-server:24.8
    container_name: clickhouse
    restart: unless-stopped

    # Expose to your laptop (DataGrip):
    ports:
      - "8123:8123"   # HTTP interface
      - "9000:9000"   # Native interface (DataGrip usually uses this)

    # Persistent storage:
    volumes:
      - clickhouse_data:/var/lib/clickhouse

    environment:
      # Create a custom user (recommended)
      CLICKHOUSE_USER: ch_user
      CLICKHOUSE_PASSWORD: ch_password_change_me
      CLICKHOUSE_DB: default

    networks:
      - airflow_net

volumes:
  clickhouse_data:

networks:
  airflow_net:
    external: true
YAML
```
### 4) Start ClickHouse:
```bash
cd /opt/clickhouse
docker compose up -d
docker ps | grep clickhouse
```
### 5) Test ClickHouse locally on server:
```bash
curl 'http://127.0.0.1:8123/?query=SELECT+1' -u your_user:your_password
```
### 6) Create table:
```bash
docker exec -it clickhouse clickhouse-client -u ch_user --password 'ch_password_change_me' -q "
CREATE TABLE IF NOT EXISTS default.music_history
(
  played_at DateTime64(3, 'America/Toronto'),
  song      String,
  artist    String,
  album     String,
  date      Date
)
ENGINE = ReplacingMergeTree
ORDER BY (song, artist, album, played_at);
"
```
### 7) DataGrip local connection settings:
```bash
- Use your server IP:
- Host: your_ip
- Port: 9000 (Native) or 8123 (HTTP)
- User: your_user
- Password: your_password
- Database: default
If it doesn’t connect: open firewall for ports 9000 and 8123 (Your server firewall/UFW).
```
### 8) Airflow docker-compose.yml (LocalExecutor):
Create /opt/airflow/docker-compose.yml:
```bash
cat > /opt/airflow/docker-compose.yml <<'YAML'
x-airflow-common: &airflow-common
  build: .
  environment: &airflow-env
    AIRFLOW__CORE__EXECUTOR: LocalExecutor
    AIRFLOW__CORE__LOAD_EXAMPLES: 'false'
    AIRFLOW__CORE__DAGS_ARE_PAUSED_AT_CREATION: 'true'
    AIRFLOW__CORE__FERNET_KEY: 'lZK5mlgQx5ep9HVI1Lrxcqy-7wA9FCEUXS5RV7_ysgY='
    AIRFLOW__DATABASE__SQL_ALCHEMY_CONN: postgresql+psycopg2://airflow:airflow@postgres/airflow

    # Resource limits for 4GB RAM
    AIRFLOW__CORE__PARALLELISM: 4
    AIRFLOW__CORE__DAG_CONCURRENCY: 2
    AIRFLOW__CORE__MAX_ACTIVE_RUNS_PER_DAG: 1

services:
  postgres:
    image: postgres:13
    container_name: airflow-postgres
    environment:
      POSTGRES_USER: airflow
      POSTGRES_PASSWORD: airflow
      POSTGRES_DB: airflow
    volumes:
      - airflow_pgdata:/var/lib/postgresql/data
    networks:
      - airflow_net
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U airflow"]
      interval: 5s
      retries: 10
      timeout: 5s

  airflow-webserver:
    <<: *airflow-common
    container_name: airflow-webserver
    command: webserver
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
    volumes:
      - ./dags:/opt/airflow/dags
      - ./logs:/opt/airflow/logs
      - ./plugins:/opt/airflow/plugins
    networks:
      - airflow_net
    restart: unless-stopped

  airflow-scheduler:
    <<: *airflow-common
    container_name: airflow-scheduler
    command: scheduler
    depends_on:
      postgres:
        condition: service_healthy
    volumes:
      - ./dags:/opt/airflow/dags
      - ./logs:/opt/airflow/logs
      - ./plugins:/opt/airflow/plugins
    networks:
      - airflow_net
    restart: unless-stopped

volumes:
  airflow_pgdata:

networks:
  airflow_net:
    external: true
YAML
```
### 9) Airflow Dockerfile + requirements (install libraries for DAG):
```bash
cat > /opt/airflow/Dockerfile <<'DOCKER'
FROM apache/airflow:2.8.1-python3.10
   
USER root
RUN apt-get update && apt-get install -y build-essential && apt-get clean && rm -rf /var/lib/apt/lists/*
USER airflow
   
COPY requirements.txt /requirements.txt
RUN pip install --no-cache-dir -r /requirements.txt
DOCKER
```
Create /opt/airflow/requirements.txt:
```bash
cat > /opt/airflow/requirements.txt <<'REQ'
pandas==2.2.2
psycopg2-binary==2.9.9
clickhouse-driver==0.2.9
spotipy==2.25.1
REQ
```
### 10) Create Airflow folders:
```bash
mkdir -p /opt/airflow/{dags,logs,plugins}
```
### 11) Build + start Airflow:
```bash
cd /opt/airflow
docker compose build
docker compose up -d
docker ps | grep airflow
```
### 12) Initialize Airflow DB + create admin user:
```bash
docker exec -it airflow-webserver airflow db migrate

docker exec -it airflow-webserver airflow users create \
  --username admin \
  --password admin \
  --firstname Admin \
  --lastname User \
  --role Admin \
  --email admin@example.com
```
### 13) Set Airflow Variables (needed by spotify_history DAG):
- CLICKHOUSE_CONN
- VLAD_SPOTIFY_CLIENT_ID
- VLAD_SPOTIFY_SECRET_KEY
- SPOTIFY_REFRESH_TOKEN
- SERVER_LOOPBACK
### 14) Deploy DAG to server (from your laptop)
```bash
   scp spotify_history.py root@142.93.155.48:/opt/airflow/dags/
```
### 15) Superset docker-compose.yml:
Create /opt/superset/docker-compose.yml:
```bash
cat > /opt/superset/docker-compose.yml <<'YAML'
services:
  superset-db:
    image: postgres:13
    restart: unless-stopped
    environment:
      POSTGRES_DB: superset
      POSTGRES_USER: superset
      POSTGRES_PASSWORD: superset
    volumes:
      - superset_db:/var/lib/postgresql/data
    networks:
      - airflow_net

  superset:
    image: apache/superset:3.0.0
    container_name: superset
    restart: unless-stopped
    ports:
      - "8088:8088"
    environment:
      SUPERSET_SECRET_KEY: "x3uQBz9cUQbjhcNT_CaIGYenZZbN5nRBm9nIvMvvNzQ0EOlyEOOmC6cC8KnOlgso"
      SUPERSET_CONFIG_PATH: "/app/superset_home/superset_config.py"
      GUNICORN_CMD_ARGS: "--no-sendfile"
    volumes:
      - superset_home:/app/superset_home
      - ./superset_config.py:/app/superset_home/superset_config.py:ro
    depends_on:
      - superset-db
    networks:
      - airflow_net

volumes:
  superset_home:
  superset_db:

networks:
  airflow_net:
    external: true
YAML
```
### 16) Start Superset:
```bash
cd /opt/superset
docker compose up -d
docker ps | grep superset
```
### 17) Initialize Superset + set admin password:
```bash
docker exec -it superset superset db upgrade
docker exec -it superset superset init

docker exec -it superset superset fab create-admin \
  --username admin \
  --firstname Admin \
  --lastname User \
  --email admin@example.com \
  --password admin
```

## Repository Contents

- `requirements.txt` — Python dependencies (Spotipy, Airflow providers, etc.);
- `spotify.py` - Dag that collects data from spotify listening history;
- `local_script.ipynb` — Jupyter notebook for one-time generation of Spotify refresh token.

This project highlights practical skills in API integration, ETL orchestration, containerized deployment, and analytical database management — foundational competencies for data engineering and analytics roles.

Feedback and contributions are welcome.
