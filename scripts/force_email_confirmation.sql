-- One-time: require existing password accounts to confirm email.
-- OAuth-only users (no PasswordHash) stay confirmed — Google/GitHub already verified the inbox.
-- Run against the Statify MSSQL database, then ask users to sign in once to receive the link.

UPDATE AspNetUsers
SET EmailConfirmed = 0
WHERE PasswordHash IS NOT NULL
  AND EmailConfirmed = 1;
