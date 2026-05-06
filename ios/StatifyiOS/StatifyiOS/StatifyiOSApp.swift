//
//  StatifyiOSApp.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-03.
//

import SwiftUI

@main
struct StatifyiOSApp: App {

    @State private var authManager = AuthManager()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(authManager)
                // Handle statify://oauth-callback?token=...&email=... deep links
                .onOpenURL { url in
                    guard url.scheme == "statify",
                          url.host == "oauth-callback"
                    else { return }
                    authManager.handleOAuthCallback(url: url)
                }
        }
    }
}
