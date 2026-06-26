import pandas as pd
import spotipy
from spotipy.oauth2 import SpotifyOAuth
from datetime import datetime, timedelta
from airflow.decorators import dag, task
from airflow.models import Variable
from airflow.utils.email import send_email
from sqlalchemy import create_engine, text
import requests
import time


"""
Send email alert when any Airflow task fails.
Includes:
    - DAG name
    - Task name
    - Execution timestamp
    - Direct link to task logs
Triggered via on_failure_callback in default_args.
"""
def notify_email(context):
    subject = f"Spotify DAG FAILED: {context['task_instance'].task_id}"

    body = f"""
    <h3>Task Failed</h3>

    <b>DAG:</b> {context['dag'].dag_id} <br>
    <b>Task:</b> {context['task_instance'].task_id} <br>
    <b>Execution Time:</b> {context['execution_date']} <br>

    <br>
    <a href="{context['task_instance'].log_url}"> Open Logs</a>
    """

    send_email(
        to="vladsahar27@gmail.com",
        subject=subject,
        html_content=body
    )

# ── default args ──────────────────────────────────────────────────────────────

default_args = {
    'owner': 'vladsahar',
    'depends_on_past': False,
    'email': 'vladsahar27@gmail.com',
    'email_on_failure': False,
    'email_on_retry': False,
    'on_failure_callback': notify_email,
    'retries': 2,
    'retry_delay': timedelta(minutes=3),
    'start_date': datetime(2026, 3, 25),
}

# Schedule: every 30 minutes
schedule_interval = "*/30 * * * *"

# ── connections ───────────────────────────────────────────────────────────────

MSSQL_CONN = Variable.get("MSSQL_CONN")
mssql_engine = create_engine(MSSQL_CONN, fast_executemany=True)

PG_CONN = Variable.get("PG_CONN")
pg_engine = create_engine(PG_CONN)

# ── musicbrainz session ───────────────────────────────────────────────────────

session = requests.Session()
session.headers.update({
    "User-Agent": "SpotifyStatisticsApp/1.0 (vladsahar27@gmail.com)"
})
artist_cache = {}

# ── constants ─────────────────────────────────────────────────────────────────

# MusicBrainz sometimes returns a country name as begin_area instead of a city.
# These are not granular enough — treat as unknown.
COUNTRY_LEVEL_AREAS = {
    'United States', 'Russia', 'Canada', 'France', 'Germany',
    'United Kingdom', 'Australia', 'Brazil', 'Japan', 'Netherlands',
    'Sweden', 'Colombia', 'Nigeria', 'Italy', 'Estonia', 'New Zealand',
    'Jamaica', 'Argentina', 'Belarus', 'Ukraine', 'Mexico', 'Finland',
    'Indonesia', 'Guyana', 'Puerto Rico', 'Ireland',
}

# MusicBrainz pseudo-code for "worldwide" — not a real ISO country
INVALID_COUNTRY_CODES = {'XW'}

TORONTO_TZ = 'America/Toronto'

MUSIC_HISTORY_COLS = [
    'played_at', 'song', 'artist', 'album', 'date', 'country', 'begin_area', 'user_id'
]

# Same play event = same second + track + user (Spotify returns last 50 every 30 min → overlap)
DEDUP_KEY_COLS = ['played_at', 'song', 'artist', 'album', 'user_id']

# ── normalization helpers ─────────────────────────────────────────────────────

def normalize_artist(name: str) -> str:
    """
    Normalize artist to Title Case.
    .lower().title() also merges encoding artifacts:
    JaŸ-Z and Jaÿ-Z both become Jaÿ-Z.
    """
    return name.strip().lower().title()


def normalize_text(value: str) -> str:
    """Normalize song / album: strip + lowercase."""
    return value.strip().lower()


def sanitize_country(country: str) -> str:
    """Replace invalid MusicBrainz country codes with unknown."""
    if not country or country in INVALID_COUNTRY_CODES:
        return 'unknown'
    return country


def sanitize_begin_area(area: str) -> str:
    """Replace country-level or empty begin_area with unknown."""
    if not area or area in COUNTRY_LEVEL_AREAS:
        return 'unknown'
    return area


def normalize_for_dedup_df(df: pd.DataFrame) -> pd.DataFrame:
    """Normalize fields so the same Spotify play always maps to the same dedup key."""
    df = df.copy()
    df['played_at'] = pd.to_datetime(df['played_at']).dt.floor('s')
    df['song'] = df['song'].astype(str).str.strip().str.lower()
    df['album'] = df['album'].astype(str).str.strip().str.lower()
    df['artist'] = df['artist'].astype(str).str.strip().str.lower().str.title()
    df['user_id'] = df['user_id'].astype(str)
    return df


def dedupe_dataframe(df: pd.DataFrame, label: str = "batch") -> pd.DataFrame:
    """Drop duplicate plays within a dataframe (e.g. overlap from Spotify last-50 window)."""
    if df.empty:
        return df
    df = normalize_for_dedup_df(df)
    before = len(df)
    df = df.drop_duplicates(subset=DEDUP_KEY_COLS, keep='first')
    dropped = before - len(df)
    if dropped:
        print(f"[{label}] Dropped {dropped} duplicate rows")
    return df


def xcom_records(df: pd.DataFrame) -> list:
    """JSON-serializable rows for Airflow XCom (no pandas Timestamp objects)."""
    if df.empty:
        return []
    out = df.copy()
    if 'played_at' in out.columns:
        out['played_at'] = (
            pd.to_datetime(out['played_at'], utc=True)
            .dt.strftime('%Y-%m-%dT%H:%M:%SZ')
        )
    return out.to_dict(orient='records')


def rows_not_in_existing(df_new: pd.DataFrame, df_existing: pd.DataFrame) -> pd.DataFrame:
    """Return rows from df_new whose dedup key is not already in df_existing."""
    if df_new.empty:
        return df_new
    new_n = normalize_for_dedup_df(df_new)
    if df_existing.empty:
        return new_n
    exist_n = normalize_for_dedup_df(df_existing)
    exist_keys = {
        tuple(row)
        for row in exist_n[DEDUP_KEY_COLS].itertuples(index=False, name=None)
    }
    mask = ~new_n[DEDUP_KEY_COLS].apply(
        lambda r: tuple(r) in exist_keys,
        axis=1,
    )
    return new_n.loc[mask].reset_index(drop=True)


# ── musicbrainz helpers ───────────────────────────────────────────────────────

def clean_artist_name(name: str) -> str:
    """Strip featured artists, ampersands, commas for better MB matching."""
    return (
        name.split(",")[0]
        .split("feat")[0]
        .split("&")[0]
        .strip()
    )


def safe_request(url: str, params: dict = None):
    """
    GET request with retry logic.
    Returns parsed JSON on 200, None after 3 failed attempts.
    """
    for attempt in range(3):
        try:
            response = session.get(url, params=params, timeout=5)
            if response.status_code == 200:
                return response.json()
            print(f"[MusicBrainz] Bad status {response.status_code} (attempt {attempt + 1})")
        except Exception as e:
            print(f"[MusicBrainz] Request error (attempt {attempt + 1}): {e}")
        time.sleep(1)
    return None


def get_artist_info(artist_name: str) -> dict:
    """
    Fetch country + begin_area from MusicBrainz for a given artist.

    Fixes applied:
    - Cache key uses normalized name to avoid duplicate API calls
      for the same artist with different encoding (JaŸ-Z vs Jaÿ-Z)
    - XW country code -> unknown
    - Country-level begin_area (e.g. "United States") -> unknown
    - None / empty values -> unknown
    """
    # Normalized name as cache key merges encoding variants
    cache_key = normalize_artist(artist_name)

    if cache_key in artist_cache:
        return artist_cache[cache_key]

    cleaned_name = clean_artist_name(artist_name)
    data = safe_request(
        "https://musicbrainz.org/ws/2/artist/",
        params={"query": f'artist:"{cleaned_name}"', "fmt": "json"}
    )

    result = {"country": "unknown", "begin_area": "unknown"}

    if data:
        artists = data.get("artists", [])
        if artists:
            artist = artists[0]
            country = sanitize_country(artist.get("country", ""))
            begin_area = sanitize_begin_area(
                artist.get("begin-area", {}).get("name", "") if artist.get("begin-area") else ""
            )
            result = {"country": country, "begin_area": begin_area}

    artist_cache[cache_key] = result
    time.sleep(0.5)  # respect MusicBrainz rate limit
    return result


# ── db helpers ────────────────────────────────────────────────────────────────

def get_all_users() -> list:
    """Fetch all Spotify users (user_id + refresh_token) from MSSQL."""
    with mssql_engine.connect() as conn:
        result = conn.execute(text("SELECT UserId, RefreshToken FROM dbo.SpotifyTokens"))
        return [{"user_id": row[0], "refresh_token": row[1]} for row in result.fetchall()]


def prepare_music_history_df(records: list) -> pd.DataFrame:
    """Normalize timestamps and text fields before loading into Postgres."""
    df = pd.DataFrame(records)

    # Timezone fix: convert first, then derive date
    df['played_at'] = (
        pd.to_datetime(df['played_at'], utc=True)
        .dt.tz_convert(TORONTO_TZ)
        .dt.tz_localize(None)
        .dt.floor('s')  # strip milliseconds for consistent dedup
    )
    # date derived AFTER timezone conversion
    df['date'] = df['played_at'].dt.date

    # Normalization guard
    df['song'] = df['song'].astype(str).str.strip().str.lower()
    df['album'] = df['album'].astype(str).str.strip().str.lower()
    df['artist'] = df['artist'].astype(str).str.strip().str.lower().str.title()
    df.loc[df['country'] == 'XW', 'country'] = 'unknown'

    return df[MUSIC_HISTORY_COLS]


# ── DAG ───────────────────────────────────────────────────────────────────────

@dag(
    schedule_interval=schedule_interval,
    start_date=datetime(2026, 1, 1),
    catchup=False,
    default_args=default_args,
)
def spotify_history():
    @task
    def get_users():
        users = get_all_users()
        print(f"Found {len(users)} users")
        return users

    @task
    def fetch_user(user):
        """
        Fetch last 50 played tracks for a single user from Spotify API.

        Normalization happens here so data is clean before touching storage:
        - song / album -> lowercase
        - artist -> Title Case (merges encoding artifacts via .lower().title())
        - date excluded — derived after timezone conversion in load_postgres
          to avoid UTC vs Toronto day mismatch bug
        """
        user_id = user["user_id"]
        refresh_token = user["refresh_token"]

        try:
            sp_oauth = SpotifyOAuth(
                client_id=Variable.get("VLAD_SPOTIFY_CLIENT_ID"),
                client_secret=Variable.get("VLAD_SPOTIFY_SECRET_KEY"),
                redirect_uri=Variable.get("SERVER_LOOPBACK"),
                scope="user-top-read user-read-recently-played"
            )
            token_info = sp_oauth.refresh_access_token(refresh_token)
            sp = spotipy.Spotify(auth=token_info['access_token'])
            results = sp.current_user_recently_played(limit=50)

            data = []
            for item in results.get("items", []):
                track = item["track"]
                data.append({
                    "played_at": item["played_at"],  # raw UTC string
                    "song": normalize_text(track["name"]),  # lowercase
                    "artist": normalize_artist(track["artists"][0]["name"]),  # Title Case
                    "album": normalize_text(track["album"]["name"]),  # lowercase
                    "user_id": user_id,
                })

            print(f"[{user_id}] Fetched {len(data)} tracks")
            return data

        except Exception as e:
            print(f"[{user_id}] Error: {e}")
            return []

    @task
    def combine(results):
        """Flatten per-user track lists and dedupe overlapping last-50 windows."""
        combined = [record for user_records in results for record in user_records]
        if not combined:
            print("No tracks fetched")
            return []
        df = dedupe_dataframe(pd.DataFrame(combined), label="combine")
        print(f"Combined {len(df)} unique records after dedup")
        return xcom_records(df)

    @task
    def enrich(records):
        """
        Enrich each record with country + begin_area from MusicBrainz.

        - Builds lookup for unique artists only (one API call per artist, not per row)
        - Cache key is normalized name — encoding variants share one cache entry
        - After enrichment, unknown begin_area rows are backfilled using the
          known city for the same artist within this batch (handles MB flakiness)
        """
        if not records:
            print("No data to enrich")
            return []

        df = pd.DataFrame(records)
        unique_artists = df['artist'].unique()
        print(f"Enriching {len(unique_artists)} unique artists via MusicBrainz...")

        # One API call per unique artist
        info_map = {artist: get_artist_info(artist) for artist in unique_artists}

        df['country'] = df['artist'].map(lambda a: info_map[a]['country'])
        df['begin_area'] = df['artist'].map(lambda a: info_map[a]['begin_area'])

        # Backfill unknown begin_area using known value for same artist in this batch
        artist_area_map = (
            df[df['begin_area'] != 'unknown']
            .groupby('artist')['begin_area']
            .agg(lambda x: x.value_counts().idxmax())
        )
        unknown_mask = (df['begin_area'] == 'unknown') & df['artist'].isin(artist_area_map.index)
        df.loc[unknown_mask, 'begin_area'] = df.loc[unknown_mask, 'artist'].map(artist_area_map)

        print(
            f"Enriched. unknown country: {(df['country'] == 'unknown').sum()}, "
            f"unknown begin_area: {(df['begin_area'] == 'unknown').sum()}"
        )

        return df.to_dict(orient="records")

    @task
    def load_postgres(records):
        """
        Insert enriched records into PostgreSQL (Superset analytics store).

        Fixes:
        - played_at converted UTC -> Toronto BEFORE deriving date
          (prevents late-night Toronto plays showing as the next UTC day)
        - Normalization guard re-applies cleanup in case anything slipped through
        - XW country -> unknown
        - Dedup before insert + ON CONFLICT DO NOTHING after
        """
        if not records:
            print("No new data to load.")
            return

        df = prepare_music_history_df(records)
        df = dedupe_dataframe(df, label="load_postgres")

        raw_conn = pg_engine.raw_connection()
        cursor = raw_conn.cursor()
        try:
            insert_sql = """
                INSERT INTO music_history
                    (played_at, song, artist, album, date, country, begin_area, user_id)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (played_at, song, artist, album, user_id) DO NOTHING
            """
            data = [tuple(row) for row in df.itertuples(index=False)]
            cursor.executemany(insert_sql, data)
            raw_conn.commit()
            print(f"Inserted up to {len(data)} rows into Postgres")
        finally:
            cursor.close()
            raw_conn.close()

    @task
    def fix_artist_country_conflicts():
        """
        Fix artists that have multiple countries / begin_area.
        Updates them using majority (most frequent value).
        """
        raw_conn = pg_engine.raw_connection()
        cursor = raw_conn.cursor()
        try:
            cursor.execute("""
                SELECT artist
                FROM music_history
                WHERE country != 'unknown'
                GROUP BY artist
                HAVING COUNT(DISTINCT country) > 1
                    OR COUNT(DISTINCT begin_area) > 1
            """)
            artists = [row[0] for row in cursor.fetchall()]

            if not artists:
                return

            print(f"Found {len(artists)} artists with conflicts")

            cursor.execute("""
                WITH counts AS (
                    SELECT artist, country, begin_area, COUNT(*) AS cnt
                    FROM music_history
                    WHERE artist = ANY(%s)
                      AND country != 'unknown'
                    GROUP BY artist, country, begin_area
                ),
                ranked AS (
                    SELECT artist, country, begin_area,
                           ROW_NUMBER() OVER (PARTITION BY artist ORDER BY cnt DESC) AS rn
                    FROM counts
                )
                SELECT artist, country, begin_area
                FROM ranked
                WHERE rn = 1
            """, (artists,))
            majority_rows = cursor.fetchall()

            print(f"Computed majority for {len(majority_rows)} artists")

            for artist, country, begin_area in majority_rows:
                cursor.execute("""
                    UPDATE music_history
                    SET country = %s, begin_area = %s
                    WHERE artist = %s
                """, (country, begin_area, artist))

            raw_conn.commit()
            print("Artist country conflicts resolved")
        finally:
            cursor.close()
            raw_conn.close()

    @task
    def load_mssql():
        """
        Append rows from Postgres into MSSQL — never truncate.

        Dedup: Spotify returns the last 50 plays every 30 minutes, so consecutive
        runs overlap. Postgres already dedupes on insert (ON CONFLICT); here we
        only insert plays whose key is not yet in MSSQL.
        """
        with pg_engine.connect() as conn:
            df_pg = pd.read_sql(
                f"SELECT {', '.join(MUSIC_HISTORY_COLS)} FROM music_history",
                conn
            )

        if df_pg.empty:
            print("Postgres is empty, nothing to sync")
            return

        df_ms = pd.read_sql(
            f"SELECT {', '.join(MUSIC_HISTORY_COLS)} FROM dbo.music_history",
            mssql_engine.raw_connection()
        )

        only_in_pg = rows_not_in_existing(df_pg, df_ms)

        if only_in_pg.empty:
            print("MSSQL already in sync — no new plays to insert")
            return

        insert_sql = """
            INSERT INTO dbo.music_history
                (played_at, song, artist, album, date, country, begin_area, user_id)
            SELECT ?, ?, ?, ?, ?, ?, ?, ?
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.music_history m
                WHERE m.user_id = ?
                  AND m.played_at = ?
                  AND ISNULL(m.song, N'') = ISNULL(?, N'')
                  AND ISNULL(m.artist, N'') = ISNULL(?, N'')
                  AND ISNULL(m.album, N'') = ISNULL(?, N'')
            )
        """

        conn = mssql_engine.raw_connection()
        cursor = conn.cursor()
        inserted = 0
        try:
            for row in only_in_pg[MUSIC_HISTORY_COLS].itertuples(index=False):
                played_at, song, artist, album, date, country, begin_area, user_id = row
                played_at = pd.Timestamp(played_at).floor('s').to_pydatetime()
                params = (
                    played_at, song, artist, album, date, country, begin_area, user_id,
                    user_id, played_at, song, artist, album,
                )
                cursor.execute(insert_sql, params)
                inserted += cursor.rowcount
            conn.commit()
            print(
                f"MSSQL sync: {len(only_in_pg)} candidate rows, "
                f"{inserted} inserted (duplicates skipped)"
            )
        finally:
            cursor.close()
            conn.close()

    # ── pipeline ──────────────────────────────────────────────────────────────
    users = get_users()
    fetched = fetch_user.expand(user=users)
    combined = combine(fetched)
    enriched = enrich(combined)

    pg = load_postgres(enriched)
    fix = fix_artist_country_conflicts()
    ms = load_mssql()

    pg >> fix >> ms


spotify_dag = spotify_history()
