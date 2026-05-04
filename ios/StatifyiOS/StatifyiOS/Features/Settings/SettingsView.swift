//
//  SettingsView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct SettingsView: View {
    @Environment(AuthManager.self) private var authManager

    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()
            VStack(spacing: 24) {
                Text("Settings")
                    .font(.syne(24, weight: .bold))
                    .foregroundColor(.appTextPrimary)

                Button("Log Out") {
                    authManager.logout()
                }
                .buttonStyle(SecondaryButtonStyle())
                .padding(.horizontal, 24)
            }
        }
    }
}
