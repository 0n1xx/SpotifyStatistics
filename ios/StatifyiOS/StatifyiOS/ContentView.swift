//
//  ContentView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-03.
//

import SwiftUI

struct ContentView: View {
    @Environment(AuthManager.self) private var authManager

    var body: some View {
        Group {
            if !authManager.isSessionReady {
                ZStack {
                    Color.appBackground.ignoresSafeArea()
                    ProgressView()
                        .tint(.appAccent)
                }
            } else if authManager.isLoggedIn {
                MainTabView()
            } else {
                LoginView()
            }
        }
        .preferredColorScheme(.dark)
        .task {
            await authManager.validateSession()
        }
    }
}

#Preview {
    ContentView()
        .environment(AuthManager())
}
