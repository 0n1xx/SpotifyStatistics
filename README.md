# SpotifyStatistics

End-to-end data pipeline for Spotify analytics: ingestion, enrichment via MusicBrainz API, scalable storage with ClickHouse, and client applications.

---

## Project Structure

### DataComponent
Contains the full data pipeline:
- Data extraction from Spotify API
- Data enrichment (MusicBrainz / external sources)
- Transformation and filtering
- Loading into ClickHouse and Microsoft SQL Server

---

## Backend & Web (Planned)

A future ASP.NET web application will be added:

- Spotify OAuth authentication
- User-specific data access
- Visualization of listening statistics
- Training interface for analyzing music preferences (e.g., trends, genres, behavior)

Microsoft SQL Server will be used as the primary data source for the web backend.

---

## Mobile App (Planned)

An iOS application (Swift) is planned:

- Connects to the ASP.NET backend
- Displays user listening insights
- Provides interactive analytics and personalized stats

---

## Vision

The goal of this project is to evolve from a data pipeline into a full ecosystem:

Pipeline → Backend → Web App → Mobile App

---

This project demonstrates skills in data engineering, backend development, and full-stack system design.
