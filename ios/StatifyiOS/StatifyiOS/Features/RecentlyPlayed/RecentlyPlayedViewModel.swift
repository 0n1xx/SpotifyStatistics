//
//  RecentlyPlayedViewModel.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation
import Observation

// @MainActor guarantees all property mutations happen on the main thread,
// which is required for @Observable classes used by SwiftUI.
// Without this, setting errorMessage/tracks from an async context causes
// a runtime warning in iOS 17+ and a crash in strict concurrency (iOS 18/Swift 6).
@Observable
@MainActor
final class RecentlyPlayedViewModel {

    var tracks: [TrackHistory] = []
    var isLoading: Bool = false
    var isLoadingMore: Bool = false
    var errorMessage: String? = nil
    var searchText: String = ""

    private var currentPage: Int = 1
    private var hasNextPage: Bool = true

    // MARK: - Initial Load
    // Called on first appear and on pull-to-refresh.
    // Resets all state so the UI starts fresh on each full reload.
    func load() async {
        // Reset state before starting — this ensures a clean slate on retry
        errorMessage = nil
        tracks = []
        currentPage = 1
        hasNextPage = true
        isLoading = true
        defer { isLoading = false }

        do {
            let response: HistoryResponse = try await APIClient.shared.get(
                path: "/api/history?page=1&limit=50"
            )
            tracks = response.tracks
            hasNextPage = response.hasNextPage
            currentPage = 1
        } catch {
            errorMessage = "Failed to load history."
        }
    }

    // MARK: - Load More (infinite scroll)
    // Called when user scrolls to the bottom of the list
    func loadMore() async {
        guard hasNextPage && !isLoadingMore else { return }

        isLoadingMore = true
        defer { isLoadingMore = false }

        do {
            let nextPage = currentPage + 1
            let response: HistoryResponse = try await APIClient.shared.get(
                path: "/api/history?page=\(nextPage)&limit=50"
            )
            // Append new tracks to existing list
            tracks += response.tracks
            hasNextPage = response.hasNextPage
            currentPage = nextPage
        } catch {
            // Silent fail on load more — user can scroll up and retry
        }
    }

    // MARK: - Filtered Tracks
    // Client-side search filter
    var filteredTracks: [TrackHistory] {
        if searchText.isEmpty { return tracks }
        return tracks.filter {
            $0.song.localizedCaseInsensitiveContains(searchText) ||
            $0.artist.localizedCaseInsensitiveContains(searchText) ||
            $0.album.localizedCaseInsensitiveContains(searchText)
        }
    }
}
