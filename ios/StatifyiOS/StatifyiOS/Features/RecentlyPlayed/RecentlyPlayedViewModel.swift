//
//  RecentlyPlayedViewModel.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation
import Observation

@Observable
final class RecentlyPlayedViewModel {

    var tracks: [TrackHistory] = []
    var isLoading: Bool = false
    var isLoadingMore: Bool = false
    var errorMessage: String? = nil
    var searchText: String = ""

    private var currentPage: Int = 1
    private var hasNextPage: Bool = true

    // MARK: - Initial Load
    func load() async {
        isLoading = true
        errorMessage = nil
        currentPage = 1
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
