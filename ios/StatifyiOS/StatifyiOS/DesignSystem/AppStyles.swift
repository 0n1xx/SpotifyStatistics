//
//  AppStyles.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

// MARK: - Card Style
// Reusable dark card background used across all screens.
// Usage: AnyView().cardStyle()
struct CardModifier: ViewModifier {
    func body(content: Content) -> some View {
        content
            .background(Color.appCard)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
    }
}

// MARK: - Primary Button Style
// Green accent button — used for main actions (Login, Connect Spotify etc.)
// Usage: Button("Login") {}.buttonStyle(PrimaryButtonStyle())
struct PrimaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.dmSans(16, weight: .bold))
            .foregroundColor(.black)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(Color.appAccent.opacity(configuration.isPressed ? 0.7 : 1))
            .cornerRadius(10)
    }
}

// MARK: - Secondary Button Style
// Outlined button — used for less prominent actions (Cancel, Sign in with Google etc.)
struct SecondaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.dmSans(16, weight: .bold))
            .foregroundColor(.appTextPrimary)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(Color.clear)
            .cornerRadius(10)
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
            .opacity(configuration.isPressed ? 0.6 : 1)
    }
}

// MARK: - Section Header Style
// Consistent heading style used above lists and chart sections.
// Usage: Text("Top Tracks").sectionHeader()
struct SectionHeaderModifier: ViewModifier {
    func body(content: Content) -> some View {
        content
            .font(.syne(20, weight: .bold))
            .foregroundColor(.appTextPrimary)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

// MARK: - View Extensions
// Shortcuts so you don't have to write .modifier(CardModifier()) everywhere
extension View {
    func cardStyle() -> some View {
        modifier(CardModifier())
    }

    func sectionHeader() -> some View {
        modifier(SectionHeaderModifier())
    }
}
