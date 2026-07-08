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

    // Formats ISO play time into the user-selected display timezone
    // (Settings → Time zone). DB values are unchanged.
    var formattedDate: String {
        DisplayTimeZone.formatPlayedAt(playedAt)
    }
}
