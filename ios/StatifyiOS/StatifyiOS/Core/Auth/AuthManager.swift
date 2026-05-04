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
    // On app launch we check Keychain to restore session.
    // If a token exists the user doesn't need to log in again.
    init() {
        self.isLoggedIn = KeychainManager.shared.isLoggedIn
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
            currentUser = response.user

            // This triggers ContentView to switch to MainTabView
            isLoggedIn = true

        } catch APIError.unauthorized {
            errorMessage = "Incorrect email or password."
        } catch APIError.serverError(let code) {
            errorMessage = "Server error (\(code)). Please try again."
        } catch {
            errorMessage = "Something went wrong. Check your connection."
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
            currentUser = response.user
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
    let user: User
}

// MARK: - User Model
// Represents the logged in user across the whole app.
struct User: Decodable {
    let id: String
    let email: String
    let displayName: String?  // Optional — user may not have set one yet
}
