//
//  DashboardView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI
import Charts

struct DashboardView: View {

    @Environment(AuthManager.self) private var authManager
    @State private var viewModel = DashboardViewModel()

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                if viewModel.isLoading && viewModel.stats == nil {
                    // First load — show full screen spinner
                    ProgressView()
                        .tint(.appAccent)
                        .scaleEffect(1.5)

                } else if let error = viewModel.errorMessage {
                    // Error state
                    VStack(spacing: 16) {
                        Image(systemName: "wifi.slash")
                            .font(.system(size: 40))
                            .foregroundColor(.appTextSecondary)
                        Text(error)
                            .font(.dmSans(16))
                            .foregroundColor(.appTextSecondary)
                        Button("Retry") {
                            Task { await viewModel.load() }
                        }
                        .buttonStyle(PrimaryButtonStyle())
                        .frame(width: 120)
                    }

                } else {
                    // Main content
                    ScrollView {
                        VStack(spacing: 24) {

                            // MARK: Range Picker
                            rangePicker

                            // MARK: Summary Cards
                            if let stats = viewModel.stats {
                                summaryCards(stats: stats)

                                // MARK: Top Tracks
                                topList(
                                    title: "Top Tracks",
                                    items: stats.topTracks.map {
                                        ListItem(primary: $0.name, secondary: $0.artist, count: $0.count)
                                    }
                                )

                                // MARK: Top Artists
                                topList(
                                    title: "Top Artists",
                                    items: stats.topArtists.map {
                                        ListItem(primary: $0.name, secondary: $0.country, count: $0.count)
                                    }
                                )

                                // MARK: Top Albums
                                topList(
                                    title: "Top Albums",
                                    items: stats.topAlbums.map {
                                        ListItem(primary: $0.name, secondary: $0.artist, count: $0.count)
                                    }
                                )
                            }

                            // MARK: Listening by Hour
                            if !viewModel.timeOfDay.isEmpty {
                                timeOfDayChart
                            }

                            // MARK: Activity by Month
                            if !viewModel.activity.isEmpty {
                                activityChart
                            }
                        }
                        .padding(16)
                    }
                    .refreshable {
                        await viewModel.load()
                    }
                }
            }
            .navigationTitle("Dashboard")
            .navigationBarTitleDisplayMode(.large)
            .toolbarColorScheme(.dark, for: .navigationBar)
        }
        .task {
            await viewModel.load()
        }
        .onChange(of: viewModel.selectedRange) {
            Task { await viewModel.load() }
        }
    }

    // MARK: - Range Picker
    private var rangePicker: some View {
        HStack(spacing: 8) {
            ForEach(viewModel.ranges, id: \.self) { range in
                Button(viewModel.label(for: range)) {
                    viewModel.selectedRange = range
                }
                .font(.dmSans(13, weight: .bold))
                .padding(.horizontal, 12)
                .padding(.vertical, 6)
                .background(
                    viewModel.selectedRange == range
                    ? Color.appAccent
                    : Color.appCard
                )
                .foregroundColor(
                    viewModel.selectedRange == range
                    ? .black
                    : .appTextSecondary
                )
                .cornerRadius(20)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    // MARK: - Summary Cards
    private func summaryCards(stats: StatsResponse) -> some View {
        LazyVGrid(columns: [
            GridItem(.flexible()),
            GridItem(.flexible())
        ], spacing: 12) {
            StatCard(title: "Total Plays", value: "\(stats.totalTracks)", icon: "music.note")
            StatCard(title: "Artists", value: "\(stats.uniqueArtists)", icon: "person.fill")
            StatCard(title: "Albums", value: "\(stats.uniqueAlbums)", icon: "square.stack.fill")
            StatCard(title: "Countries", value: "\(stats.uniqueCountries)", icon: "globe")
        }
    }

    // MARK: - Top List
    private struct ListItem {
        let primary: String
        let secondary: String
        let count: Int
    }

    private func topList(title: String, items: [ListItem]) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(title).sectionHeader()

            VStack(spacing: 1) {
                ForEach(Array(items.enumerated()), id: \.offset) { index, item in
                    HStack(spacing: 12) {
                        // Rank number
                        Text("\(index + 1)")
                            .font(.dmSans(13, weight: .bold))
                            .foregroundColor(.appTextSecondary)
                            .frame(width: 20, alignment: .trailing)

                        // Name + subtitle
                        VStack(alignment: .leading, spacing: 2) {
                            Text(item.primary)
                                .font(.dmSans(15, weight: .bold))
                                .foregroundColor(.appTextPrimary)
                                .lineLimit(2)       // Long feat. titles wrap instead of truncating
                                .fixedSize(horizontal: false, vertical: true)
                            Text(item.secondary)
                                .font(.dmSans(13))
                                .foregroundColor(.appTextSecondary)
                                .lineLimit(1)
                        }

                        Spacer()

                        // Play count
                        Text("\(item.count)")
                            .font(.dmSans(14, weight: .bold))
                            .foregroundColor(.appAccent)
                    }
                    .padding(12)
                    .background(Color.appCard)
                }
            }
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
        }
    }

    // MARK: - Time of Day Chart
    private var timeOfDayChart: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Listening by Hour").sectionHeader()

            Chart {
                ForEach(Array(viewModel.timeOfDay.enumerated()), id: \.offset) { hour, count in
                    BarMark(
                        x: .value("Hour", hour),
                        y: .value("Plays", count)
                    )
                    .foregroundStyle(Color.appAccent)
                    .cornerRadius(3)
                }
            }
            .frame(height: 160)
            .chartXScale(domain: 0...23)
            .chartXAxis {
                AxisMarks(values: [0, 6, 12, 18, 23]) { value in
                    AxisGridLine(stroke: StrokeStyle(lineWidth: 0.3))
                        .foregroundStyle(Color.appBorder)
                    AxisValueLabel {
                        if let h = value.as(Int.self) {
                            Text(hourLabel(h))
                                .font(.dmSans(10))
                                .foregroundStyle(Color.appTextSecondary)
                        }
                    }
                }
            }
            .chartYAxis(.hidden)
            .padding(16)
            .background(Color.appCard)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
        }
    }

    private func hourLabel(_ hour: Int) -> String {
        switch hour {
        case 0:  return "12am"
        case 6:  return "6am"
        case 12: return "12pm"
        case 18: return "6pm"
        case 23: return "11pm"
        default: return "\(hour)"
        }
    }

    // MARK: - Activity Chart
    private var activityChart: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Activity by Month").sectionHeader()

            Chart(viewModel.activity) { month in
                // BarMark works with any number of data points (1, 2, N).
                // LineMark+AreaMark with .catmullRom is invisible for a single point —
                // that's why the chart appeared empty when the user only has data for Apr 2026.
                BarMark(
                    x: .value("Month", month.month),
                    y: .value("Plays", month.count)
                )
                .foregroundStyle(Color.appAccent)
                .cornerRadius(3)
            }
            .chartXScale(range: .plotDimension(padding: 8))
            .frame(height: 160)
            .chartXAxis {
                // Show every month label, rotated so they don't overlap
                AxisMarks { value in
                    AxisValueLabel(orientation: .verticalReversed)
                        .font(.dmSans(10))
                        .foregroundStyle(Color.appTextSecondary)
                }
            }
            .chartYAxis(.hidden)
            .padding(16)
            .background(Color.appCard)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
        }
    }
}

// MARK: - Stat Card
// Reusable summary card for the 2x2 grid at the top of Dashboard
struct StatCard: View {
    let title: String
    let value: String
    let icon: String

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Image(systemName: icon)
                .font(.system(size: 20))
                .foregroundColor(.appAccent)

            Text(value)
                .font(.syne(28, weight: .bold))
                .foregroundColor(.appTextPrimary)

            Text(title)
                .font(.dmSans(13))
                .foregroundColor(.appTextSecondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .cardStyle()
    }
}
