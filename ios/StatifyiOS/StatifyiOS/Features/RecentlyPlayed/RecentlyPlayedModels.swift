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

    // Formats "2025-12-31T00:20:52.0000000" → "Dec 31, 12:20 AM"
    // The C# .ToString("o") format outputs fractional seconds but no timezone suffix,
    // so we use a plain DateFormatter with a custom format string instead of ISO8601DateFormatter.
    var formattedDate: String {
        let fmt = DateFormatter()
        // Match C# "o" format: yyyy-MM-ddTHH:mm:ss.fffffff (no timezone)
        fmt.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS"
        fmt.locale = Locale(identifier: "en_US_POSIX")
        fmt.timeZone = TimeZone(identifier: "UTC")

        if let date = fmt.date(from: playedAt) {
            let display = DateFormatter()
            display.dateStyle = .medium
            display.timeStyle = .short
            display.timeZone = TimeZone.current
            return display.string(from: date)
        }

        // Fallback: try fewer fractional second digits (e.g. .000)
        fmt.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSS"
        if let date = fmt.date(from: playedAt) {
            let display = DateFormatter()
            display.dateStyle = .medium
            display.timeStyle = .short
            display.timeZone = TimeZone.current
            return display.string(from: date)
        }

        return playedAt
    }
}
