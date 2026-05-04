//
//  AppColors.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI
// MARK: - App Color Palette
// All colors used in Statify are defined here.
extension Color {
    // Main screen background — almost black
    static let appBackground = Color(hex: "#080808")
    // Card and surface color — slightly lighter than background
    static let appCard = Color(hex: "#111111")
    // Spotify green — used for buttons, highlights, active states
    static let appAccent = Color(hex: "#1DB954")
    // Primary text — white
    static let appTextPrimary = Color.white
    // Secondary text — muted grey for subtitles, metadata
    static let appTextSecondary = Color(hex: "#999999")
    // Subtle border for cards and dividers
    static let appBorder = Color(hex: "#222222")
}

// MARK: - Hex Color Initializer
// Lets you create a Color from a hex string like "#1DB954"
extension Color {
    init(hex: String) {
        // Strip any non-hex characters (like "#")
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)

        // Extract R, G, B components from the 24-bit hex value
        let r = Double((int >> 16) & 0xFF) / 255
        let g = Double((int >> 8) & 0xFF) / 255
        let b = Double(int & 0xFF) / 255

        self.init(red: r, green: g, blue: b)
    }
}
