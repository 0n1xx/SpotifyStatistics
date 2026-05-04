//
//  ContentView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-03.
//

import SwiftUI

struct ContentView: View {

    @State private var authManager = AuthManager()

    var body: some View {
        Group {
            if authManager.isLoggedIn {
                Text("Welcome, \(authManager.currentUser?.email ?? "")!")
                    .foregroundColor(.appAccent)
                    .font(.syne(24, weight: .bold))
            } else {
                LoginView()
            }
        }
        .environment(authManager)
        .preferredColorScheme(.dark)
    }
}
#Preview {
    ContentView()
}
