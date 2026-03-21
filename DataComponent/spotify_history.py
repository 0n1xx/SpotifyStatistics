import pandas as pd
import spotipy
from spotipy.oauth2 import SpotifyOAuth
from datetime import datetime, timedelta
from airflow import DAG
from airflow.models import Variable
from airflow.operators.python import PythonOperator
from clickhouse_driver import Client
from urllib.parse import urlparse
from sqlalchemy import create_engine, text
import requests
import time

default_args = {
    'owner': 'vladsahar',
    'depends_on_past': False,
    'email': 'vladsahar27@gmail.com',
    'email_on_failure': True,
    'email_on_retry': False,
    'retries': 2,
    'retry_delay': timedelta(minutes=3),
    'start_date': datetime(2026, 1, 1, 15, 0),
}

# Schedule: every hour
schedule_interval = "0 * * * *"

# Get the full connection string from Airflow Variable
conn_string = Variable.get("CLICKHOUSE_CONN")

# Parse it
url = urlparse(conn_string)

# Create ClickHouse client
client = Client(
    host=url.hostname,
    port=url.port or 9000,
    user=url.username,
    password=url.password,
    database=url.path.lstrip("/") or "default"
)

# SQL Microsoft server connection
MSSQL_CONN = Variable.get("MSSQL_CONN")

mssql_engine = create_engine(
    MSSQL_CONN,
    fast_executemany=True
)

# Creating a session for musicbrainz api
session = requests.Session()
HEADERS = {
    "User-Agent": "SpotifyStatisticsApp/1.0 (vladsahar27@gmail.com)"
}
session.headers.update(HEADERS)
artist_cache = {}

# A function that gives a clear artist name
def clean_artist_name(name):
    return (
        name.split(",")[0]
            .split("feat")[0]
            .split("&")[0]
            .strip()
    )

"""
Send an HTTP request with basic retry logic.

Steps:
1. Try requesting up to 3 times.
2. Return JSON if the response status is 200.
3. Log errors or bad status codes.
4. Wait between retries to avoid hitting rate limits.
5. Return None if all attempts fail.
"""
def safe_request(url, params=None):
    for _ in range(3):
        try:
            response = session.get(url, params=params, timeout=5)

            if response.status_code == 200:
                return response.json()

            print("Bad status:", response.status_code)

        except Exception as e:
            print("Request error:", e)

        time.sleep(1)

    return None

"""
Fetch artist metadata from MusicBrainz API.

Steps:
1. Check the cache to avoid duplicate API calls.
2. Clean artist name (remove "feat", "&", etc.) for better matching.
3. Send request to MusicBrainz API.
4. Extract:
   - country (ISO code, e.g., CA, US)
   - begin_area (city of origin)
5. Return "unknown" if data is missing or the request fails.
6. Cache result and apply a small delay to respect API rate limits.
"""
def get_artist_info(artist_name):
    if artist_name in artist_cache:
        return artist_cache[artist_name]

    cleaned_name = clean_artist_name(artist_name)

    url = "https://musicbrainz.org/ws/2/artist/"
    params = {
        "query": f'artist:"{cleaned_name}"',
        "fmt": "json"
    }

    data = safe_request(url, params)

    if not data:
        result = {"country": "unknown", "begin_area": "unknown"}
        artist_cache[artist_name] = result
        return result

    artists = data.get("artists", [])

    if not artists:
        result = {"country": "unknown", "begin_area": "unknown"}
        artist_cache[artist_name] = result
        return result

    artist = artists[0]

    country = artist.get("country", "unknown")
    begin_area = artist.get("begin-area", {}).get("name") if artist.get("begin-area") else "unknown"

    result = {"country": "unknown", "begin_area": "unknown"}

    artist_cache[artist_name] = result

    time.sleep(0.5)  # rate limit

    return result

# DAG definition
dag = DAG(
    'spotify_history',
    default_args=default_args,
    schedule_interval=schedule_interval,
    catchup=False
)

def fetch_spotify_history(ti):
    sp_oauth = SpotifyOAuth(
        client_id=Variable.get("VLAD_SPOTIFY_CLIENT_ID"),
        client_secret=Variable.get("VLAD_SPOTIFY_SECRET_KEY"),
        redirect_uri=Variable.get("SERVER_LOOPBACK"),
        scope="user-top-read user-read-recently-played"
    )

    refresh_token = Variable.get("SPOTIFY_REFRESH_TOKEN")
    token_info = sp_oauth.refresh_access_token(refresh_token)
    token = token_info['access_token']

    sp = spotipy.Spotify(auth=token)

    results = sp.current_user_recently_played(limit=50)
    data = []

    for item in results.get("items", []):
        track = item["track"]
        played_at = pd.to_datetime(item["played_at"]).tz_convert("America/Toronto")
        data.append({
            "played_at": played_at.isoformat(),
            "song": track["name"],
            "artist": track["artists"][0]["name"],
            "album": track["album"]["name"],
            "date": played_at.date().isoformat()
        })
    df = pd.DataFrame(data)

    ti.xcom_push(key="spotify_history", value=df.to_dict(orient="records"))

def enrich_artist_data(ti):
    records = ti.xcom_pull(
        key="spotify_history",
        task_ids="fetch_spotify_history"
    )
    if not records:
        print("No data to enrich")
        return
    df = pd.DataFrame(records)

    countries = []
    begin_areas = []

    # Looping through every artist to find their country, country code and city
    for artist in df['artist']:
        info = get_artist_info(artist)
        countries.append(info['country'])
        begin_areas.append(info['begin_area'])
    df['country'] = countries
    df['begin_area'] = begin_areas
    df['user_id'] = 'admin'

    ti.xcom_push(key="spotify_history_enriched", value=df.to_dict(orient="records"))

def load_to_clickhouse(ti):
    records = ti.xcom_pull(
        key="spotify_history_enriched",
        task_ids="enrich_artist_data"
    )

    if not records:
        print("No new data to load.")
        return

    df = pd.DataFrame(records)
    # Converting columns to the proper date format
    df['played_at'] = pd.to_datetime(df['played_at'])
    df['date'] = pd.to_datetime(df['date']).dt.date

    # Changing the order of the columns, so it follows the order in the databse
    df = df[['played_at', 'song', 'artist', 'album', 'date', 'country', 'begin_area', 'user_id']]

    client.execute(
        "INSERT INTO default.music_history VALUES",
        df.to_dict(orient="records")
    )

    client.execute("""
        OPTIMIZE TABLE default.music_history
        DEDUPLICATE BY song, album, artist, played_at
    """)

def load_to_mssql(ti):
    records = ti.xcom_pull(
        key="spotify_history_enriched",
        task_ids="enrich_artist_data"
    )

    if not records:
        print("No data to load to MSSQL")
        return

    df = pd.DataFrame(records)

    df['played_at'] = pd.to_datetime(df['played_at'], utc=True)
    df['date'] = pd.to_datetime(df['date']).dt.date
    df['played_at'] = df['played_at'].dt.tz_convert(None)
    df = df[['played_at', 'song', 'artist', 'album', 'date', 'country', 'begin_area', 'user_id']]

    df.to_sql(
        "music_history",
        mssql_engine,
        schema="dbo",
        if_exists="append",
        index=False,
        method="multi"
    )

    merge_sql = """
        MERGE dbo.music_history AS target
        USING dbo.music_history_staging AS source
        ON target.song = source.song
           AND target.artist = source.artist
           AND target.album = source.album
           AND target.played_at = source.played_at

        WHEN NOT MATCHED THEN
            INSERT (played_at, song, artist, album, date, country, begin_area, user_id)
            VALUES (source.played_at, source.song, source.artist, source.album,
                    source.date, source.country, source.begin_area, source.user_id);
        """

    with mssql_engine.begin() as conn:
        conn.execute(text(merge_sql))

    with mssql_engine.begin() as conn:
        conn.execute(text("DROP TABLE dbo.music_history_staging"))

fetch_spotify_history_task = PythonOperator(
    task_id="fetch_spotify_history",
    python_callable=fetch_spotify_history,
    dag=dag,
    do_xcom_push=True
)

enrich_artist_data_task = PythonOperator(
    task_id="enrich_artist_data",
    python_callable=enrich_artist_data,
    dag=dag
)

load_to_clickhouse_task = PythonOperator(
    task_id="load_to_clickhouse",
    python_callable=load_to_clickhouse,
    dag=dag,
    do_xcom_push=False
)

load_to_mssql_task = PythonOperator(
    task_id="load_to_mssql",
    python_callable=load_to_mssql,
    dag=dag
)

fetch_spotify_history_task >> enrich_artist_data_task >> [load_to_clickhouse_task, load_to_mssql_task]
