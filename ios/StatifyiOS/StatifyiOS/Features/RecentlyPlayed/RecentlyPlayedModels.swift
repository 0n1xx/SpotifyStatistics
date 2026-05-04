//
//  RecentlyPlayedModels.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation

// MARK: - History Response
// Maps to GET /api/history
struct HistoryResponse: Decodable {
    let page: Int
    let limit: Int
    let totalCount: Int
    let totalPages: Int
    let hasNextPage: Bool
    let tracks: [TrackHistory]
}

// MARK: - Track History
struct TrackHistory: Decodable, Identifiable {
    // playedAt is unique enough to use as id
    var id: String { playedAt }
    let song: String
    let artist: String
    let album: String
    let country: String
    let playedAt: String

    // Formats "2026-05-04T16:52:38.0000000+00:00" → "May 4, 4:52 PM"
    // GetDateTimeOffset().ToString("o") produces full ISO-8601 with timezone offset,
    // which ISO8601DateFormatter handles with .withInternetDateTime + .withFractionalSeconds.
    // timeStyle = .short gives "4:52 PM" — no seconds, no milliseconds.
    var formattedDate: String {
        let iso = ISO8601DateFormatter()
        iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]

        if let date = iso.date(from: playedAt) {
            let display = DateFormatter()
            display.dateStyle = .medium
            display.timeStyle = .short
            display.timeZone = TimeZone.current
            return display.string(from: date)
        }

        // Fallback: without fractional seconds
        iso.formatOptions = [.withInternetDateTime]
        if let date = iso.date(from: playedAt) {
            let display = DateFormatter()
            display.dateStyle = .medium
            display.timeStyle = .short
            display.timeZone = TimeZone.current
            return display.string(from: date)
        }

        return playedAt
    }
}
