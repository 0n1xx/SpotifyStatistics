//
//  DashboardModels.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation

// MARK: - Dashboard Response
// Maps to GET /api/stats response from the server
struct StatsResponse: Decodable {
    let totalTracks: Int
    let uniqueArtists: Int
    let uniqueAlbums: Int
    let uniqueCountries: Int
    let topTracks: [TrackStat]
    let topArtists: [ArtistStat]
    let topAlbums: [AlbumStat]
}

// MARK: - Track
struct TrackStat: Decodable, Identifiable {
    // Identifiable нужен для ForEach в SwiftUI
    // id вычисляется из name+artist так как сервер не возвращает id
    var id: String { name + artist }
    let name: String
    let artist: String
    let count: Int
}

// MARK: - Artist
struct ArtistStat: Decodable, Identifiable {
    var id: String { name }
    let name: String
    let country: String
    let count: Int
}

// MARK: - Album
struct AlbumStat: Decodable, Identifiable {
    var id: String { name + artist }
    let name: String
    let artist: String
    let count: Int
}

// MARK: - Time of Day Response
// Maps to GET /api/timeofday
struct TimeOfDayResponse: Decodable {
    let hours: [Int]  // Array of 24 ints — one per hour
}

// MARK: - Activity Response
// Maps to GET /api/activity
struct ActivityResponse: Decodable {
    let months: [MonthStat]
}

struct MonthStat: Decodable, Identifiable {
    var id: String { month }
    let month: String
    let count: Int
}
