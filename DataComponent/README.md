# Spotify Listening History Analytics Pipeline

## Project Overview

This project implements an automated ETL pipeline for collecting, storing, and analyzing personal Spotify listening history.

Data is extracted from the Spotify API, processed, and loaded into databases for analytics and visualization.

---

## Example Dashboard

![Dashboard](dashboardExample.png)

## Architecture

* **Apache Airflow** – orchestrates ETL pipeline
* **Spotipy** – Spotify API integration
* **ClickHouse** – analytical database
* **Microsoft SQL Server** – additional relational storage
* **Apache Superset** – dashboards and visualization
* **Docker** – containerized deployment

---

## Data Collected

* Playback timestamp
* Track name
* Artist
* Album
* Duration and metadata

---

## Databases

### ClickHouse

Used for fast analytical queries and dashboards.

### Microsoft SQL Server

Used as an additional relational database for structured storage. It will also be used as a backend data source for a future web application (serving processed data to the frontend).

Example table:

```sql
CREATE TABLE spotify_tracks (
    id INT IDENTITY(1,1) PRIMARY KEY,
    track_name NVARCHAR(255),
    artist_name NVARCHAR(255),
    album_name NVARCHAR(255),
    release_date DATE,
    popularity INT,
    duration_ms INT,
    explicit BIT,
    artist_country NVARCHAR(100),
    created_at DATETIME DEFAULT GETDATE()
);
```

Data is continuously appended (only new tracks are inserted).

---

## Pipeline Logic

1. Fetch recently played tracks from Spotify API
2. Filter only new records
3. Enrich data (e.g., artist country)
4. Load into:

   * ClickHouse
   * Microsoft SQL Server

---

## Quick Start

### Prerequisites

* Docker & Docker Compose
* Spotify Developer Account

### Setup

```bash
git clone https://github.com/0n1xx/SpotifyStatistics.git
cd project
docker compose up -d
```

### Airflow

* Open: [http://localhost:8080](http://localhost:8080)
* Trigger the DAG to start data collection

---

## Superset

* Open: [http://localhost:8088](http://localhost:8088)
* Connect to ClickHouse
* Build dashboards

---

## Repository Contents

* `spotify.py` — Airflow DAG
* `requirements.txt` — dependencies
* `local_script.ipynb` — generate Spotify refresh token

---

## Notes

* Data is appended incrementally (no duplicates)
* Some fields may contain `unknown` values

---

This project demonstrates practical skills in ETL pipelines, APIs, databases, and data visualization.
