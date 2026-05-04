//
//  WorldMapView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

// MARK: - WorldMapView
// Shows where in the world the user has been listening to music.
// Displays a ranked list of countries with a heat bar showing relative play count.
struct WorldMapView: View {

    @State private var viewModel = WorldMapViewModel()

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                if viewModel.isLoading {
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

                } else if viewModel.countries.isEmpty {
                    // Empty state
                    VStack(spacing: 12) {
                        Image(systemName: "globe")
                            .font(.system(size: 48))
                            .foregroundColor(.appTextSecondary)
                        Text("No location data yet")
                            .font(.syne(18, weight: .bold))
                            .foregroundColor(.appTextPrimary)
                        Text("Start listening on Spotify\nto see where your music comes from.")
                            .font(.dmSans(14))
                            .foregroundColor(.appTextSecondary)
                            .multilineTextAlignment(.center)
                    }
                    .padding(32)

                } else {
                    ScrollView {
                        VStack(spacing: 20) {

                            // MARK: Summary Header
                            summaryHeader

                            // MARK: Country List
                            VStack(alignment: .leading, spacing: 12) {
                                Text("Countries")
                                    .sectionHeader()

                                VStack(spacing: 1) {
                                    ForEach(Array(viewModel.countries.enumerated()), id: \.element.id) { index, stat in
                                        CountryRow(
                                            rank: index + 1,
                                            stat: stat,
                                            maxCount: viewModel.maxCount,
                                            totalPlays: viewModel.totalPlays
                                        )

                                        if index < viewModel.countries.count - 1 {
                                            Divider()
                                                .background(Color.appBorder)
                                                .padding(.horizontal, 16)
                                        }
                                    }
                                }
                                .background(Color.appCard)
                                .cornerRadius(12)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 12)
                                        .stroke(Color.appBorder, lineWidth: 1)
                                )
                            }
                        }
                        .padding(16)
                    }
                    .refreshable {
                        await viewModel.load()
                    }
                }
            }
            .navigationTitle("World Map")
            .navigationBarTitleDisplayMode(.large)
            .toolbarColorScheme(.dark, for: .navigationBar)
        }
        .task {
            await viewModel.load()
        }
    }

    // MARK: - Summary Header
    // 2-column grid showing total plays and unique countries at a glance.
    private var summaryHeader: some View {
        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
            StatCard(
                title: "Countries",
                value: "\(viewModel.countries.count)",
                icon: "globe"
            )
            StatCard(
                title: "Total Plays",
                value: "\(viewModel.totalPlays)",
                icon: "music.note"
            )
        }
    }
}

// MARK: - Country Row
// One row per country: rank, flag + name, heat bar, play count, percentage.
private struct CountryRow: View {
    let rank: Int
    let stat: CountryStat
    let maxCount: Int
    let totalPlays: Int

    // How full the heat bar is — proportional to the top country.
    private var barFraction: CGFloat {
        guard maxCount > 0 else { return 0 }
        return CGFloat(stat.count) / CGFloat(maxCount)
    }

    // Percentage of total plays this country represents.
    private var percentage: String {
        guard totalPlays > 0 else { return "0%" }
        let pct = Double(stat.count) / Double(totalPlays) * 100
        return String(format: "%.1f%%", pct)
    }

    var body: some View {
        VStack(spacing: 8) {
            HStack(spacing: 12) {
                // Rank
                Text("\(rank)")
                    .font(.dmSans(13, weight: .bold))
                    .foregroundColor(.appTextSecondary)
                    .frame(width: 20, alignment: .trailing)

                // Flag + country name
                Text(stat.flag)
                    .font(.system(size: 24))

                VStack(alignment: .leading, spacing: 2) {
                    Text(stat.fullName)
                        .font(.dmSans(15, weight: .bold))
                        .foregroundColor(.appTextPrimary)
                        .lineLimit(1)

                    // ISO code as subtitle
                    Text(stat.country)
                        .font(.dmSans(11))
                        .foregroundColor(.appTextSecondary)
                }

                Spacer()

                VStack(alignment: .trailing, spacing: 2) {
                    // Play count
                    Text("\(stat.count)")
                        .font(.dmSans(15, weight: .bold))
                        .foregroundColor(.appAccent)

                    // Percentage
                    Text(percentage)
                        .font(.dmSans(11))
                        .foregroundColor(.appTextSecondary)
                }
            }

            // Heat bar — shows relative weight vs top country
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    // Background track
                    RoundedRectangle(cornerRadius: 2)
                        .fill(Color.appBorder)
                        .frame(height: 3)

                    // Filled portion — green scaled to max
                    RoundedRectangle(cornerRadius: 2)
                        .fill(Color.appAccent)
                        .frame(width: geo.size.width * barFraction, height: 3)
                }
            }
            .frame(height: 3)
            .padding(.leading, 44) // align with country name, not rank number
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }
}
