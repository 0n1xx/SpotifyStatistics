-- Dedup constraints for music_history (Postgres + MSSQL)
-- Run once on each database. Safe to re-run.

-- ── PostgreSQL (Airflow / Superset) ───────────────────────────────────────────
-- Required for: ON CONFLICT (played_at, song, artist, album, user_id) DO NOTHING

-- CREATE UNIQUE INDEX IF NOT EXISTS ux_music_history_dedup
-- ON music_history (user_id, played_at, song, artist, album);

-- ── MSSQL (Statify web app) ───────────────────────────────────────────────────
-- Remove duplicate plays (keep one row per user + timestamp + track)

;WITH ranked AS (
    SELECT
        played_at, song, artist, album, user_id,
        ROW_NUMBER() OVER (
            PARTITION BY user_id, played_at, song, artist, album
            ORDER BY played_at
        ) AS rn
    FROM dbo.music_history
)
DELETE FROM ranked WHERE rn > 1;

GO
