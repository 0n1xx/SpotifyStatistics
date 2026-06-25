-- Run in Postgres (Superset SQL Lab, psql, or pgAdmin)
-- Export result as CSV with header row.

SELECT
    played_at,
    song,
    artist,
    album,
    date,
    country,
    begin_area,
    user_id
FROM music_history
ORDER BY played_at;

-- psql example:
-- \copy (SELECT played_at, song, artist, album, date, country, begin_area, user_id FROM music_history ORDER BY played_at) TO 'music_history.csv' WITH CSV HEADER
