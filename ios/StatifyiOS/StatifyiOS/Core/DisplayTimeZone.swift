//
//  DisplayTimeZone.swift
//  StatifyiOS
//
//  Client-only display timezone preference.
//  DB timestamps stay as stored; this only changes how times look on screen.
//

import Foundation

enum DisplayTimeZone {
    static let storageKey = "statify.displayTimeZone"

    struct Option: Identifiable, Hashable {
        let id: String
        let label: String
    }

    /// Short curated pool — enough for most friends (Toronto / Egypt / EU / US).
    static let options: [Option] = [
        .init(id: "America/Toronto", label: "Toronto (Eastern)"),
        .init(id: "America/New_York", label: "New York (Eastern)"),
        .init(id: "America/Chicago", label: "Chicago (Central)"),
        .init(id: "America/Denver", label: "Denver (Mountain)"),
        .init(id: "America/Los_Angeles", label: "Los Angeles (Pacific)"),
        .init(id: "America/Sao_Paulo", label: "São Paulo"),
        .init(id: "UTC", label: "UTC"),
        .init(id: "Europe/London", label: "London"),
        .init(id: "Europe/Berlin", label: "Berlin / Paris / Rome"),
        .init(id: "Europe/Moscow", label: "Moscow"),
        .init(id: "Africa/Cairo", label: "Cairo (Egypt)"),
        .init(id: "Africa/Johannesburg", label: "Johannesburg"),
        .init(id: "Asia/Dubai", label: "Dubai"),
        .init(id: "Asia/Kolkata", label: "India"),
        .init(id: "Asia/Tokyo", label: "Tokyo"),
        .init(id: "Australia/Sydney", label: "Sydney"),
    ]

    static var currentId: String {
        get {
            if let saved = UserDefaults.standard.string(forKey: storageKey), !saved.isEmpty {
                return saved
            }
            return TimeZone.current.identifier
        }
        set {
            UserDefaults.standard.set(newValue, forKey: storageKey)
        }
    }

    static var current: TimeZone {
        TimeZone(identifier: currentId) ?? TimeZone(identifier: "America/Toronto") ?? .current
    }

    static func formatPlayedAt(_ iso: String) -> String {
        let isoFmt = ISO8601DateFormatter()
        isoFmt.formatOptions = [.withInternetDateTime, .withFractionalSeconds]

        let date: Date? = {
            if let d = isoFmt.date(from: iso) { return d }
            isoFmt.formatOptions = [.withInternetDateTime]
            return isoFmt.date(from: iso)
        }()

        guard let date else { return iso }

        let display = DateFormatter()
        display.dateStyle = .medium
        display.timeStyle = .short
        display.timeZone = current
        return display.string(from: date)
    }
}
