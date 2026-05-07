//
//  WorldMapModels.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation

// MARK: - Map Response
// Maps to GET /api/map response from the server.
// Returns play counts grouped by country code.
struct MapResponse: Decodable {
    let countries: [CountryStat]
}

// MARK: - Country Stat
// A single country and how many tracks were played there.
struct CountryStat: Decodable, Identifiable {
    // ISO 3166-1 alpha-2 code, e.g. "US", "CA", "GB"
    let country: String
    let count: Int

    // Identifiable conformance — country code is unique per user
    var id: String { country }

    // Full country name derived from the ISO code using Apple's Locale API.
    // Locale.current ensures it uses the device language.
    var fullName: String {
        Locale.current.localizedString(forRegionCode: country) ?? country
    }

    // Country flag emoji derived from ISO code.
    // Each letter maps to a Regional Indicator Symbol (Unicode U+1F1E6–U+1F1FF).
    // Combining two of them produces a flag emoji: "U" + "S" → 🇺🇸
    var flag: String {
        country.unicodeScalars.compactMap {
            Unicode.Scalar(127397 + $0.value)
        }.map(String.init).joined()
    }
}
