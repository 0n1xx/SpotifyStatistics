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

    // Formats "2026-03-24T16:27:47" into "Mar 24, 4:27 PM"
    var formattedDate: String {
        let iso = ISO8601DateFormatter()
        iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        guard let date = iso.date(from: playedAt) else { return playedAt }
        let fmt = DateFormatter()
        fmt.dateStyle = .medium
        fmt.timeStyle = .short
        return fmt.string(from: date)
    }
}
