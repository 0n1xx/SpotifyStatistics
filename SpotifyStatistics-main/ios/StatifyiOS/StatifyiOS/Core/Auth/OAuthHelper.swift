//
//  OAuthHelper.swift
//  StatifyiOS
//

import AuthenticationServices
import UIKit

// MARK: - WindowProvider
// ASWebAuthenticationSession needs a UIWindow to present from.
// This singleton grabs the key window and provides it as the anchor.
final class WindowProvider: NSObject, ASWebAuthenticationPresentationContextProviding {

    static let shared = WindowProvider()
    private override init() {}

    func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap { $0.windows }
            .first { $0.isKeyWindow }
            ?? UIWindow()
    }
}

// MARK: - startOAuth (free function)
// Call from any view — LoginView, RegisterView, SettingsView.
// Keeps a strong reference to the session so ARC doesn't kill it mid-flow.
private var _activeSession: ASWebAuthenticationSession?

func startOAuth(provider: String, authManager: AuthManager) {
    let base = "https://spotifystatistics-production.up.railway.app"
    let urlString = "\(base)/Identity/Account/ExternalLogin?provider=\(provider)&mobile=true"
    guard let url = URL(string: urlString) else { return }

    let session = ASWebAuthenticationSession(
        url: url,
        callbackURLScheme: "statify"
    ) { callbackURL, error in
        _activeSession = nil
        guard let callbackURL, error == nil else { return }
        authManager.handleOAuthCallback(url: callbackURL)
    }
    session.prefersEphemeralWebBrowserSession = true
    session.presentationContextProvider = WindowProvider.shared
    _activeSession = session
    session.start()
}
