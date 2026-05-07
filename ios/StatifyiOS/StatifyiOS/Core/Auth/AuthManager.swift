//
//  AuthManager.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation
import Observation

// MARK: - AuthManager
// The central source of truth for authentication state.
// All screens observe this — when isLoggedIn changes, the UI updates automatically.
// @Observable is Swift's modern alternative to ObservableObject (iOS 17+)
@Observable
final class AuthManager {

    // MARK: - State
    // isLoggedIn drives what ContentView shows.
    // When this changes → UI automatically re-renders.
    var isLoggedIn: Bool = false

    // True while a login/register request is in flight.
    // Used to show a loading spinner on the button.
    var isLoading: Bool = false

    // Holds the current error message to display to the user.
    // nil = no error, any String = show error banner.
    var errorMessage: String? = nil

    // The currently logged in user — populated after login/register.
    var currentUser: User? = nil

    // MARK: - Init
    // On app launch: restore session from Keychain.
    // If a token exists, decode the email from the JWT payload so Settings
    // can pre-fill the email field immediately — without waiting for /api/profile.
    init() {
        self.isLoggedIn = KeychainManager.shared.isLoggedIn
        if isLoggedIn, let token = KeychainManager.shared.getToken() {
            self.currentUser = User(id: "", email: Self.emailFromJWT(token) ?? "", displayName: nil)
        }
    }

    // Decodes the email claim from a JWT without verifying the signature.
    // Safe here because we only use it for display — the server still validates
    // the full token on every API call.
    private static func emailFromJWT(_ token: String) -> String? {
        let parts = token.components(separatedBy: ".")
        guard parts.count == 3 else { return nil }
        // JWT payload is base64url encoded — pad to multiple of 4
        var base64 = parts[1]
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        let remainder = base64.count % 4
        if remainder > 0 { base64 += String(repeating: "=", count: 4 - remainder) }
        guard let data = Data(base64Encoded: base64),
              let json  = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let email = json["email"] as? String else { return nil }
        return email
    }

    // MARK: - Login
    // Sends email + password to the backend.
    // On success: saves token, sets currentUser, flips isLoggedIn.
    // On failure: sets errorMessage for the UI to display.
    func login(email: String, password: String) async {

        // Clear any previous error and show loading state
        errorMessage = nil
        isLoading = true

        // defer runs this block when the function exits — success or failure.
        // Guarantees isLoading is always reset even if we throw.
        defer { isLoading = false }

        do {
            // Build the request body
            let body = LoginRequest(email: email, password: password)

            // Send POST /api/auth/login → decode into AuthResponse
            let response: AuthResponse = try await APIClient.shared.post(
                path: "/api/auth/login",
                body: body
            )

            // Save the JWT token securely in Keychain
            KeychainManager.shared.saveToken(response.token)

            // Store the user so screens can access their name, email etc.
            currentUser = User(id: "", email: response.email, displayName: nil)

            // This triggers ContentView to switch to MainTabView
            isLoggedIn = true

        } catch APIError.serverError(let code) {
            errorMessage = "Server error: \(code)"
        } catch APIError.decodingFailed {
            errorMessage = "Decoding failed"
        } catch APIError.unauthorized {
            errorMessage = "Unauthorized"
        } catch {
            errorMessage = "Error: \(error.localizedDescription)"
        }
    }

    // MARK: - Register
    // Creates a new account and logs in immediately on success.
    func register(email: String, password: String) async {

        errorMessage = nil
        isLoading = true
        defer { isLoading = false }

        do {
            let body = RegisterRequest(email: email, password: password)

            let response: AuthResponse = try await APIClient.shared.post(
                path: "/api/auth/register",
                body: body
            )

            KeychainManager.shared.saveToken(response.token)
            currentUser = User(id: "", email: response.email, displayName: nil)
            isLoggedIn = true

        } catch APIError.serverError(409) {
            // 409 Conflict — email already registered
            errorMessage = "An account with this email already exists."
        } catch {
            errorMessage = "Something went wrong. Check your connection."
        }
    }

    // MARK: - Logout
    // Clears the token and resets all state.
    // ContentView will switch back to LoginView automatically.
    func logout() {
        KeychainManager.shared.deleteToken()
        currentUser = nil
        isLoggedIn = false
    }

    // MARK: - OAuth Deep-link Callback
    // Called from StatifyiOSApp when the app receives a statify://oauth-callback URL.
    // Extracts token + email from query params and logs the user in.
    func handleOAuthCallback(url: URL) {
        guard let components = URLComponents(url: url, resolvingAgainstBaseURL: false),
              let token = components.queryItems?.first(where: { $0.name == "token" })?.value,
              let email = components.queryItems?.first(where: { $0.name == "email" })?.value
        else { return }

        KeychainManager.shared.saveToken(token)
        currentUser = User(id: "", email: email, displayName: nil)
        isLoggedIn = true
    }
}

// MARK: - Request Models
// Codable structs that get encoded to JSON for the request body.
// Encodable = can be converted TO JSON.

struct LoginRequest: Encodable {
    let email: String
    let password: String
}

struct RegisterRequest: Encodable {
    let email: String
    let password: String
}

// MARK: - Response Models
// Decodable structs that get decoded FROM the server's JSON response.

struct AuthResponse: Decodable {
    let token: String
    let expiresAt: String
    let email: String
}

// MARK: - User Model
// Represents the logged in user across the whole app.
struct User: Decodable {
    let id: String
    let email: String
    let displayName: String?  // Optional — user may not have set one yet
}
