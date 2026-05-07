//
//  KeychainManager.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//
import Foundation
import Security

// MARK: - KeychainManager
// Secure storage for the JWT token.
// Keychain is iOS's encrypted storage — far safer than UserDefaults.
// UserDefaults stores data in plain text — never store tokens there.
final class KeychainManager {

    // Single shared instance — same Singleton pattern as APIClient
    static let shared = KeychainManager()
    private init() {}

    // The key under which the token is stored in Keychain
    private let tokenKey = "statify_jwt_token"

    // MARK: - Save Token
    // Called after a successful login to persist the JWT token.
    func saveToken(_ token: String) {

        // Convert the token string to raw bytes
        let data = Data(token.utf8)

        // Query is a dictionary of parameters for the Keychain API.
        // Keychain is a low-level C API — it uses dictionaries to describe operations.
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,  // Type of record
            kSecAttrAccount as String: tokenKey,            // Key (identifier)
            kSecValueData as String: data                   // Value (the token bytes)
        ]

        // Delete the existing token first to avoid duplicate entry errors
        SecItemDelete(query as CFDictionary)

        // Store the new token
        SecItemAdd(query as CFDictionary, nil)
    }

    // MARK: - Get Token
    // Retrieves the stored JWT token to attach to API requests.
    // Returns nil if the user is not logged in.
    func getToken() -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: tokenKey,
            kSecReturnData as String: true,         // We want the actual data back
            kSecMatchLimit as String: kSecMatchLimitOne  // Return only one result
        ]

        // result is an inout parameter — Keychain writes the found item into it
        var result: AnyObject?
        SecItemCopyMatching(query as CFDictionary, &result)

        // Cast the result to Data and convert back to a String
        guard let data = result as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    // MARK: - Delete Token
    // Called on logout to remove the token from Keychain.
    func deleteToken() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: tokenKey
        ]
        SecItemDelete(query as CFDictionary)
    }

    // MARK: - Is Logged In
    // Computed property — returns true if a token exists in Keychain.
    // Used in ContentView to decide whether to show LoginView or MainTabView.
    var isLoggedIn: Bool {
        getToken() != nil
    }
}
