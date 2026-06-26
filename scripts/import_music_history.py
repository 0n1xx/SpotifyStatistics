#!/usr/bin/env python3
"""
Import music_history from a Postgres CSV export into MSSQL.

CSV columns (header required):
  played_at, song, artist, album, date, country, begin_area, user_id

Usage:
  pip install pymssql pandas

  python scripts/import_music_history.py \\
    --csv music_history.csv \\
    --server db44161.public.databaseasp.net \\
    --database db44161 \\
    --user db44161 \\
    --password 'YOUR_PASSWORD' \\
    --truncate

  # After re-registering on Statify, remap all rows to your new Identity user id:
  python scripts/import_music_history.py --csv music_history.csv ... --user-id "new-guid-here"
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pandas as pd
import pymssql

COLUMNS = [
    "played_at",
    "song",
    "artist",
    "album",
    "date",
    "country",
    "begin_area",
    "user_id",
]


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Import music_history CSV into MSSQL")
    p.add_argument("--csv", required=True, type=Path, help="Path to CSV file")
    p.add_argument("--server", required=True, help="MSSQL host (host or host,port)")
    p.add_argument("--database", required=True, help="Database name")
    p.add_argument("--user", required=True)
    p.add_argument("--password", required=True)
    p.add_argument(
        "--truncate",
        action="store_true",
        help="DELETE all rows in music_history before import",
    )
    p.add_argument(
        "--user-id",
        help="Replace every user_id in the CSV with this value (use after new Statify registration)",
    )
    p.add_argument(
        "--skip-existing",
        action="store_true",
        help="Skip rows that already exist (matched by played_at, song, artist, album, user_id)",
    )
    p.add_argument("--batch-size", type=int, default=500)
    return p.parse_args()


def load_csv(path: Path, replace_user_id: str | None) -> pd.DataFrame:
    df = pd.read_csv(path)
    # Postgres exports may include an `id` column — ignore it
    extra = [c for c in df.columns if c not in COLUMNS and c != "id"]
    if extra:
        print(f"Note: ignoring extra CSV columns: {extra}")
    missing = [c for c in COLUMNS if c not in df.columns]
    if missing:
        sys.exit(f"CSV missing columns: {missing}. Expected: {COLUMNS}")

    df = df[COLUMNS].copy()
    df["played_at"] = pd.to_datetime(df["played_at"]).dt.floor("s")
    df["date"] = pd.to_datetime(df["date"], errors="coerce").dt.date

    for col in ("song", "artist", "album", "country", "begin_area"):
        df[col] = df[col].astype(str).replace({"nan": None, "None": None})

    df["user_id"] = df["user_id"].astype(str)
    if replace_user_id:
        df["user_id"] = replace_user_id

    before = len(df)
    df = df.drop_duplicates(subset=["played_at", "song", "artist", "album", "user_id"])
    print(f"Rows in CSV: {before}, after dedup: {len(df)}")
    return df


def connect(server: str, database: str, user: str, password: str) -> pymssql.Connection:
    return pymssql.connect(
        server=server,
        user=user,
        password=password,
        database=database,
        login_timeout=30,
    )


def ensure_table(conn: pymssql.Connection) -> None:
    cur = conn.cursor()
    cur.execute("SELECT 1 FROM sys.tables WHERE name = 'music_history'")
    if not cur.fetchone():
        sys.exit(
            "Table music_history does not exist. "
            "Run scripts/mssql_full_setup.sql on this database first."
        )
    cur.close()


def existing_keys(conn: pymssql.Connection) -> set[tuple[str, str, str, str, str]]:
    cur = conn.cursor()
    cur.execute("SELECT played_at, song, artist, album, user_id FROM music_history")
    keys: set[tuple[str, str, str, str, str]] = set()
    for played_at, song, artist, album, user_id in cur.fetchall():
        ts = pd.Timestamp(played_at).floor("s")
        keys.add((str(ts), str(song), str(artist), str(album), str(user_id)))
    cur.close()
    return keys


def import_rows(
    conn: pymssql.Connection,
    df: pd.DataFrame,
    batch_size: int,
    truncate: bool,
    skip_existing: bool,
) -> None:
    cur = conn.cursor()
    if truncate:
        cur.execute("DELETE FROM music_history")
        conn.commit()
        print("Truncated music_history")

    if skip_existing:
        keys = existing_keys(conn)
        before = len(df)
        df = df[
            ~df.apply(
                lambda r: (
                    str(r.played_at),
                    str(r.song),
                    str(r.artist),
                    str(r.album),
                    str(r.user_id),
                )
                in keys,
                axis=1,
            )
        ]
        print(f"Skipped {before - len(df)} existing rows, inserting {len(df)}")

    if df.empty:
        print("Nothing new to insert")
        cur.close()
        return

    sql = """
        INSERT INTO music_history
            (played_at, song, artist, album, date, country, begin_area, user_id)
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
    """

    rows = [
        (
            row.played_at.to_pydatetime() if hasattr(row.played_at, "to_pydatetime") else row.played_at,
            row.song,
            row.artist,
            row.album,
            row.date,
            row.country if row.country not in (None, "nan") else None,
            row.begin_area if row.begin_area not in (None, "nan") else None,
            row.user_id,
        )
        for row in df.itertuples(index=False)
    ]

    total = 0
    for i in range(0, len(rows), batch_size):
        batch = rows[i : i + batch_size]
        cur.executemany(sql, batch)
        conn.commit()
        total += len(batch)
        print(f"Inserted {total}/{len(rows)}")

    cur.execute("SELECT COUNT(*) FROM music_history")
    print(f"Total rows in music_history: {cur.fetchone()[0]}")
    cur.close()


def main() -> None:
    args = parse_args()
    if not args.csv.is_file():
        sys.exit(f"File not found: {args.csv}")

    df = load_csv(args.csv, args.user_id)
    if df.empty:
        sys.exit("CSV has no rows after parsing")

    conn = connect(args.server, args.database, args.user, args.password)
    try:
        ensure_table(conn)
        import_rows(conn, df, args.batch_size, args.truncate, args.skip_existing)
    finally:
        conn.close()

    print("Done.")


if __name__ == "__main__":
    main()
