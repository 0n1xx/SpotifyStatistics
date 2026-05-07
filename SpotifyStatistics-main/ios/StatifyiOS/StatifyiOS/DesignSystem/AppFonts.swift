//
//  AppFonts.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

// MARK: - App Typography
// Statify uses two custom fonts:
// - Syne: headings, titles, prominent labels
// - DM Sans: body text, subtitles, metadata

extension Font {

    // Syne — geometric, bold feel for headings
    // Example: Text("Dashboard").font(.syne(28, weight: .bold))
    static func syne(_ size: CGFloat, weight: Font.Weight = .regular) -> Font {
        let name = weight == .bold ? "Syne-Bold" : "Syne-Regular"
        return .custom(name, size: size)
    }

    // DM Sans — clean, readable for body and UI text
    // Example: Text("3 min ago").font(.dmSans(13))
    static func dmSans(_ size: CGFloat, weight: Font.Weight = .regular) -> Font {
        let name = weight == .bold ? "DMSans_24pt-Bold" : "DMSans_18pt-Regular"
        return .custom(name, size: size)
    }
}
