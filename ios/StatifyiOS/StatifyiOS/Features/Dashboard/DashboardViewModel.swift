//
//  DashboardViewModel.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation
import Observation

// MARK: - DashboardViewModel
// Fetches and holds all data for the Dashboard screen.
// @Observable means SwiftUI will re-render DashboardView when any var changes.
// @MainActor guarantees all property mutations happen on the main thread,
// which is required for @Observable classes used by SwiftUI.
@Observable
@MainActor
final class DashboardViewModel {

    // MARK: - State
    var stats: StatsResponse? = nil
    var timeOfDay: [Int] = []       // 24 values — plays per hour
    var activity: [MonthStat] = []  // Monthly play counts

    var isLoading: Bool = false
    var errorMessage: String? = nil

    // Current time range filter
    var selectedRange: String = "all"
    let ranges = ["7d", "30d", "6m", "all"]

    // MARK: - Load
    // Fetches all three endpoints concurrently using async let.
    // async let fires all requests at the same time — faster than sequential.
    func load() async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            // Fire all 3 requests simultaneously
            async let statsRequest: StatsResponse = APIClient.shared.get(
                path: "/api/stats?range=\(selectedRange)"
            )
            async let timeRequest: TimeOfDayResponse = APIClient.shared.get(
                path: "/api/timeofday?range=\(selectedRange)"
            )
            async let activityRequest: ActivityResponse = APIClient.shared.get(
                path: "/api/activity?range=\(selectedRange)"
            )

            // Wait for all three to complete
            let (statsResult, timeResult, activityResult) = try await (
                statsRequest,
                timeRequest,
                activityRequest
            )

            stats = statsResult
            timeOfDay = timeResult.hours
            activity = activityResult.months

        } catch {
            errorMessage = "Failed to load dashboard data."
        }
    }

    // MARK: - Range Label
    // Human readable label for the range picker
    func label(for range: String) -> String {
        switch range {
        case "7d":  return "7 Days"
        case "30d": return "30 Days"
        case "6m":  return "6 Months"
        default:    return "All Time"
        }
    }
}
