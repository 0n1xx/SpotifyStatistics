-- Statify MSSQL setup (run on a fresh or empty database)
-- Identity tables (AspNetUsers, etc.) are created by the web app on deploy (dotnet ef / Program.cs Migrate).
-- Run this script for music history + Statify-specific tables if the web app has not migrated yet.

-- ── App tables (skip if web migrations already ran) ───────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SpotifyTokens')
CREATE TABLE SpotifyTokens (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(MAX) NOT NULL,
    AccessToken NVARCHAR(MAX) NOT NULL,
    RefreshToken NVARCHAR(MAX) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserProfiles')
CREATE TABLE UserProfiles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    AvatarBase64 NVARCHAR(MAX) NULL,
    PhoneNumber NVARCHAR(20) NULL,
    DisplayName NVARCHAR(100) NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DataProtectionKeys')
CREATE TABLE DataProtectionKeys (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FriendlyName NVARCHAR(MAX) NULL,
    Xml NVARCHAR(MAX) NULL
);

-- ── Listening history (same schema as Postgres + Airflow pipeline) ──────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'music_history')
CREATE TABLE music_history (
    played_at   DATETIME2     NOT NULL,
    song        NVARCHAR(500) NULL,
    artist      NVARCHAR(500) NULL,
    album       NVARCHAR(500) NULL,
    date        DATE          NULL,
    country     NVARCHAR(100) NULL,
    begin_area  NVARCHAR(200) NULL,
    user_id     NVARCHAR(450) NOT NULL
);

-- Optional: index for dashboard queries per user
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_music_history_user_played_at')
CREATE INDEX IX_music_history_user_played_at
    ON music_history (user_id, played_at DESC);

GO
