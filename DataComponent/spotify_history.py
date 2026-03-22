import pandas as pd
import spotipy
from spotipy.oauth2 import SpotifyOAuth
from datetime import datetime, timedelta
from airflow.decorators import dag, task
from airflow.models import Variable
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

# Connection to Spotify ASP.NET Database
MSSQL_CONN_SPOTIFY = Variable.get("MSSQL_CONN_SPOTIFY")
spotify_engine = create_engine(MSSQL_CONN_SPOTIFY, fast_executemany=True)

# Master DB (history)
MSSQL_CONN_MASTER = Variable.get("MSSQL_CONN_MASTER")
master_engine = create_engine(MSSQL_CONN_MASTER, fast_executemany=True)

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

    result = {
        "country": country or "unknown",
        "begin_area": begin_area or "unknown"
    }

    artist_cache[artist_name] = result

    time.sleep(0.5)  # rate limit

    return result

"""
Fetch all Spotify users from MSSQL.

Steps:
1. Connect to SpotifyStatisticsDb using spotify_engine.
2. Read UserId and RefreshToken from dbo.SpotifyTokens.
3. Transform query result into a list of dictionaries.
4. Return list of users for downstream processing.

Output:
[
    {"user_id": "...", "refresh_token": "..."},
    ...
]
"""
def get_all_users():
    query = "SELECT UserId, RefreshToken FROM dbo.SpotifyTokens"

    with spotify_engine.connect() as conn:
        result = conn.execute(text(query))
        users = result.fetchall()

    return [{"user_id": row[0], "refresh_token": row[1]} for row in users]


@dag(
    schedule_interval=schedule_interval,
    start_date=datetime(2026, 1, 1),
    catchup=False
)
def spotify_history():

    @task
    def get_users():
        users = get_all_users()
        print(f"USERS: {users}")
        return get_all_users()

    @task
    def fetch_user(user):
        # Requesting information for each user
        user_id = user["user_id"]
        refresh_token = user["refresh_token"]

        # Spotify API requires a user-agent header
        try:
            sp_oauth = SpotifyOAuth(
                client_id=Variable.get("VLAD_SPOTIFY_CLIENT_ID"),
                client_secret=Variable.get("VLAD_SPOTIFY_SECRET_KEY"),
                redirect_uri=Variable.get("SERVER_LOOPBACK"),
                scope="user-top-read user-read-recently-played"
            )

            token_info = sp_oauth.refresh_access_token(refresh_token)
            token = token_info['access_token']

            sp = spotipy.Spotify(auth=token)

            results = sp.current_user_recently_played(limit=50)

            data = []

            # Extracting relevant information from each track
            for item in results.get("items", []):
                track = item["track"]
                played_at = pd.to_datetime(item["played_at"]).tz_convert("America/Toronto")

                data.append({
                    "played_at": played_at.isoformat(),
                    "song": track["name"],
                    "artist": track["artists"][0]["name"],
                    "album": track["album"]["name"],
                    "date": played_at.date().isoformat(),
                    "user_id": user_id
                })

            return data

        except Exception as e:
            print(f"Error processing user {user_id}: {e}")
            return []

    @task
    def combine(results):
        combined = []
        for r in results:
            combined.extend(r)
        return combined

    @task
    def enrich(records):
        if not records:
            print("No data to enrich")
            return []

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

        return df.to_dict(orient="records")

    @task
    def load_clickhouse(records):
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
            DEDUPLICATE BY song, album, artist, played_at, user_id
        """)

    """
    Load Spotify history into MSSQL.

    Steps:
    1. Pull enriched track data.
    2. Convert timestamps and prepare DataFrame.
    3. Truncate staging table (dbo.staging_music).
    4. Bulk insert data into staging.
    5. MERGE into dbo.music_history:
       - insert only new records
       - skip duplicates
    """
    @task
    def load_mssql(records):
        if not records:
            print("No data to load to MSSQL")
            return

        df = pd.DataFrame(records)

        df['played_at'] = pd.to_datetime(df['played_at'], utc=True).dt.tz_convert(None)
        df['date'] = pd.to_datetime(df['date']).dt.date

        df = df[['played_at', 'song', 'artist', 'album', 'date', 'country', 'begin_area', 'user_id']]

        conn = master_engine.raw_connection()
        cursor = conn.cursor()

        try:
            cursor.execute("TRUNCATE TABLE dbo.staging_music")
            conn.commit()

            insert_query = """
            INSERT INTO dbo.staging_music (
                played_at, song, artist, album, date, country, begin_area, user_id
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """

            data = [tuple(row) for row in df.itertuples(index=False)]

            cursor.fast_executemany = True
            cursor.executemany(insert_query, data)
            conn.commit()

            cursor.execute("""
            MERGE dbo.music_history AS target
            USING dbo.staging_music AS source
            ON 
                target.played_at = source.played_at
                AND target.song = source.song
                AND target.artist = source.artist
                AND target.album = source.album
                AND target.user_id = source.user_id
            WHEN NOT MATCHED THEN
                INSERT (played_at, song, artist, album, date, country, begin_area, user_id)
                VALUES (source.played_at, source.song, source.artist, source.album, source.date, source.country, source.begin_area, source.user_id);
            """)
            conn.commit()

        finally:
            cursor.close()
            conn.close()

    users = get_users()
    user_data = fetch_user.expand(user=users)
    combined = combine(user_data)
    enriched = enrich(combined)

    load_clickhouse(enriched)
    load_mssql(enriched)


spotify_dag = spotify_history()