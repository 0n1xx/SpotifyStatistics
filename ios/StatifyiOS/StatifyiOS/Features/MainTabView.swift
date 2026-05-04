//
//  MainTabView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

// MARK: - MainTabView
// Root navigation after login.
// Contains 4 tabs — each maps to a main feature of the app.
struct MainTabView: View {

    @Environment(AuthManager.self) private var authManager

    var body: some View {
        TabView {

            // MARK: Dashboard
            DashboardView()
                .tabItem {
                    Label("Dashboard", systemImage: "chart.bar.fill")
                }

            // MARK: Recently Played
            RecentlyPlayedView()
                .tabItem {
                    Label("History", systemImage: "clock.fill")
                }

            // MARK: World Map
            WorldMapView()
                .tabItem {
                    Label("Map", systemImage: "globe")
                }

            // MARK: Settings
            SettingsView()
                .tabItem {
                    Label("Settings", systemImage: "gearshape.fill")
                }
        }
        .tint(.appAccent)
        // Force dark mode for all tabs
        .preferredColorScheme(.dark)
        // Hide default white tab bar background — we'll style it manually
        .onAppear {
            let appearance = UITabBarAppearance()
            appearance.configureWithOpaqueBackground()
            appearance.backgroundColor = UIColor(Color.appCard)
            UITabBar.appearance().standardAppearance = appearance
            UITabBar.appearance().scrollEdgeAppearance = appearance
        }
    }
}
