//
//  WorldMapViewModel.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation
import Observation

// MARK: - WorldMapViewModel
// Fetches country play counts from GET /api/map.
// @MainActor ensures all @Observable property mutations happen on the main thread.
@Observable
@MainActor
final class WorldMapViewModel {

    // MARK: - State
    var countries: [CountryStat] = []
    var isLoading: Bool = false
    var errorMessage: String? = nil

    // MARK: - Computed
    // Total plays across all countries — used to calculate percentages.
    var totalPlays: Int {
        countries.reduce(0) { $0 + $1.count }
    }

    // The country with the most plays — used to normalise the heat scale.
    var maxCount: Int {
        countries.map(\.count).max() ?? 1
    }

    // MARK: - Load
    func load() async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            let response: MapResponse = try await APIClient.shared.get(path: "/api/map")
            // Already sorted by count DESC from the server, but sort again defensively
            countries = response.countries.sorted { $0.count > $1.count }
        } catch APIError.unauthorized {
            errorMessage = "Session expired. Please log in again."
        } catch {
            errorMessage = "Failed to load map data."
        }
    }
}
