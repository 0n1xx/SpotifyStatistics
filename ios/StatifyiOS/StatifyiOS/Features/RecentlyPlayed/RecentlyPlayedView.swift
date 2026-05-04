//
//  RecentlyPlayedView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct RecentlyPlayedView: View {

    @State private var viewModel = RecentlyPlayedViewModel()

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                if viewModel.isLoading {
                    ProgressView()
                        .tint(.appAccent)
                        .scaleEffect(1.5)

                } else if let error = viewModel.errorMessage {
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
                    trackList
                }
            }
            .navigationTitle("Recently Played")
            .navigationBarTitleDisplayMode(.large)
            .searchable(
                text: $viewModel.searchText,
                prompt: "Search songs, artists, albums"
            )
        }
        .task {
            await viewModel.load()
        }
    }

    // MARK: - Track List
    private var trackList: some View {
        ScrollView {
            LazyVStack(spacing: 1) {
                ForEach(viewModel.filteredTracks) { track in
                    TrackRow(track: track)
                        .onAppear {
                            // Load more when last item appears
                            if track.id == viewModel.filteredTracks.last?.id {
                                Task { await viewModel.loadMore() }
                            }
                        }
                }

                // Loading more indicator
                if viewModel.isLoadingMore {
                    ProgressView()
                        .tint(.appAccent)
                        .padding(16)
                }
            }
            .padding(.horizontal, 16)
        }
        .refreshable {
            await viewModel.load()
        }
    }
}

// MARK: - Track Row
struct TrackRow: View {
    let track: TrackHistory

    var body: some View {
        HStack(spacing: 12) {

            // Music note icon
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color.appCard)
                    .frame(width: 44, height: 44)
                Image(systemName: "music.note")
                    .font(.system(size: 16))
                    .foregroundColor(.appAccent)
            }

            // Song info
            VStack(alignment: .leading, spacing: 3) {
                Text(track.song)
                    .font(.dmSans(15, weight: .bold))
                    .foregroundColor(.appTextPrimary)
                    .lineLimit(1)

                Text(track.artist)
                    .font(.dmSans(13))
                    .foregroundColor(.appTextSecondary)
                    .lineLimit(1)
            }

            Spacer()

            // Date + country flag
            VStack(alignment: .trailing, spacing: 3) {
                Text(track.formattedDate)
                    .font(.dmSans(11))
                    .foregroundColor(.appTextSecondary)

                if !track.country.isEmpty && track.country != "unknown" {
                    Text(track.country)
                        .font(.dmSans(11))
                        .foregroundColor(.appTextSecondary)
                }
            }
        }
        .padding(12)
        .background(Color.appCard)
    }
}
