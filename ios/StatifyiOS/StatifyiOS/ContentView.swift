//
//  ContentView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-03.
//

import SwiftUI

import SwiftUI

struct ContentView: View {
    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()

            VStack(spacing: 24) {

                // Title
                Text("Statify")
                    .font(.syne(32, weight: .bold))
                    .foregroundColor(.appAccent)

                // Card example
                VStack(alignment: .leading, spacing: 8) {
                    Text("Top Track")
                        .sectionHeader()
                    Text("Middle of the Ocean. — Drake")
                        .font(.dmSans(15))
                        .foregroundColor(.appTextSecondary)
                }
                .padding(16)
                .cardStyle()

                // Button examples
                Button("Connect Spotify") {}
                    .buttonStyle(PrimaryButtonStyle())

                Button("Sign in with Google") {}
                    .buttonStyle(SecondaryButtonStyle())
            }
            .padding(24)
        }
    }
}

#Preview {
    ContentView()
}
