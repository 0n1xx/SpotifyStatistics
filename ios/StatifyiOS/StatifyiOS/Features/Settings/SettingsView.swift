//
//  SettingsView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//
//
//  SettingsView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI
import PhotosUI         // Required for photo picker
import SafariServices
import AuthenticationServices  // ASWebAuthenticationSession for OAuth

struct SettingsView: View {

    @Environment(AuthManager.self) private var authManager

    // MARK: - Form State
    // These mirror the web settings fields exactly
    @State private var displayName: String = ""
    @State private var email: String = ""
    @State private var phoneNumber: String = ""

    // MARK: - UI State
    @State private var showDeleteConfirm: Bool = false      // Delete account alert
    @State private var showChangePassword: Bool = false     // Change password sheet
    @State private var showPhotoPicker: Bool = false        // Photo library picker
    @State private var selectedPhoto: PhotosPickerItem? = nil
    @State private var avatarImage: UIImage? = nil          // Selected avatar image
    @State private var saveStatus: String? = nil            // "Saved!" feedback message
    @State private var isSaving: Bool = false               // Prevents double-tap while saving

    // MARK: - Connection State
    // Loaded from GET /api/profile on every appear — reflects real server state
    @State private var spotifyConnected: Bool = false
    @State private var googleConnected:  Bool = false
    @State private var githubConnected:  Bool = false

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                ScrollView {
                    VStack(spacing: 24) {
                        profileSection
                        accountSection
                        connectedAccountsSection
                        dangerZoneSection
                    }
                    .padding(16)
                }
                // Tap anywhere outside a text field to dismiss keyboard
                .onTapGesture { hideKeyboard() }
            }
            .navigationTitle("Settings")
            .navigationBarTitleDisplayMode(.large)
        }
        // Populate fields by fetching fresh profile from server on every appear
        .onAppear {
            // Pre-fill instantly from cached user so fields aren't blank while loading
            email = authManager.currentUser?.email ?? ""
            displayName = authManager.currentUser?.displayName ?? ""
            // Then refresh from API to pick up any changes made on web or other devices
            Task {
                struct ProfileResp: Decodable {
                    let displayName: String?
                    let email: String?
                    let phoneNumber: String?
                    let avatarBase64: String?
                    let spotifyConnected: Bool?
                    let googleConnected:  Bool?
                    let githubConnected:  Bool?
                }
                if let profile: ProfileResp = try? await APIClient.shared.get(path: "/api/profile") {
                    if let n = profile.displayName { displayName = n }
                    if let e = profile.email        { email = e }
                    if let p = profile.phoneNumber  { phoneNumber = p }
                    // Update connection badges from server — the source of truth
                    spotifyConnected = profile.spotifyConnected ?? false
                    googleConnected  = profile.googleConnected  ?? false
                    githubConnected  = profile.githubConnected  ?? false
                    // Load saved avatar if user hasn't picked a new one this session
                    if avatarImage == nil, let b64 = profile.avatarBase64,
                       let url = URL(string: b64),
                       let data = try? Data(contentsOf: url),
                       let img = UIImage(data: data) {
                        avatarImage = img
                    }
                }
            }
        }
        // MARK: Change Password Sheet
        .sheet(isPresented: $showChangePassword) {
            ChangePasswordView()
                .environment(authManager)
        }
        // MARK: Delete Account Confirmation
        .alert("Delete your account?", isPresented: $showDeleteConfirm) {
            Button("Cancel", role: .cancel) {}
            Button("Yes, delete everything", role: .destructive) {
                // TODO: Call DELETE /api/account then logout
                authManager.logout()
            }
        } message: {
            Text("This will permanently delete your account and all your listening history. There is no way to recover this data.")
        }

        // MARK: Photo Selection Handler
        // Triggered when user picks a photo from the library
        .onChange(of: selectedPhoto) {
            Task {
                guard let data = try? await selectedPhoto?.loadTransferable(type: Data.self),
                      let image = UIImage(data: data) else { return }

                // Resize to max 512px to keep the base64 payload small
                let size   = image.size
                let scale  = min(512 / size.width, 512 / size.height, 1)
                let target = CGSize(width: size.width * scale, height: size.height * scale)
                let renderer = UIGraphicsImageRenderer(size: target)
                let resized  = renderer.image { _ in image.draw(in: CGRect(origin: .zero, size: target)) }

                // Convert to JPEG base64 data URL
                guard let jpeg = resized.jpegData(compressionQuality: 0.7) else { return }
                let dataURL = "data:image/jpeg;base64," + jpeg.base64EncodedString()

                avatarImage = resized

                // Upload to backend
                do {
                    struct Body: Encodable { let avatarBase64: String }
                    struct Resp: Decodable { let ok: Bool }
                    let _: Resp = try await APIClient.shared.put(
                        path: "/api/settings/avatar",
                        body: Body(avatarBase64: dataURL)
                    )
                    saveStatus = "Photo saved!"
                } catch {
                    saveStatus = "Upload failed"
                }
            }
        }
    }

    // MARK: - Profile Section
    // Shows avatar, display name and email — matches web profile card
    private var profileSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Profile").sectionHeader()

            HStack(spacing: 16) {

                // Avatar — shows selected photo or initials fallback
                PhotosPicker(selection: $selectedPhoto, matching: .images) {
                    ZStack {
                        Circle()
                            .fill(Color.appAccent)
                            .frame(width: 72, height: 72)

                        if let avatarImage {
                            // Show selected photo
                            Image(uiImage: avatarImage)
                                .resizable()
                                .scaledToFill()
                                .frame(width: 72, height: 72)
                                .clipShape(Circle())
                        } else {
                            // Fallback to first letter of email
                            Text(String(email.prefix(1)).uppercased())
                                .font(.syne(28, weight: .bold))
                                .foregroundColor(.black)
                        }

                        // Camera icon overlay — hints that photo is tappable
                        VStack {
                            Spacer()
                            HStack {
                                Spacer()
                                Image(systemName: "camera.fill")
                                    .font(.system(size: 12))
                                    .foregroundColor(.white)
                                    .padding(4)
                                    .background(Color.black.opacity(0.6))
                                    .clipShape(Circle())
                            }
                        }
                        .frame(width: 72, height: 72)
                    }
                }

                VStack(alignment: .leading, spacing: 4) {
                    Text(displayName.isEmpty ? email : displayName)
                        .font(.dmSans(16, weight: .bold))
                        .foregroundColor(.appTextPrimary)

                    Text(email)
                        .font(.dmSans(14))
                        .foregroundColor(.appTextSecondary)

                    // Upload photo button
                    PhotosPicker(selection: $selectedPhoto, matching: .images) {
                        Text("Upload photo")
                            .font(.dmSans(13))
                            .foregroundColor(.appAccent)
                    }
                    .padding(.top, 2)
                }

                Spacer()
            }
            .padding(16)
            .cardStyle()
        }
    }

    // MARK: - Account Section
    // Display name, email, phone, password — matches web Account section
    private var accountSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Account").sectionHeader()

            VStack(spacing: 0) {

                // Display Name
                SettingRow(label: "Display name", description: "Shown in your profile and sidebar. Max 50 characters.") {
                    HStack(spacing: 8) {
                        TextField("Your name", text: $displayName)
                            .font(.dmSans(15))
                            .foregroundColor(.appTextPrimary)
                            .autocorrectionDisabled()
                            .submitLabel(.done)
                            .onSubmit { hideKeyboard() }

                        Button("Save") {
                            hideKeyboard()
                            guard !isSaving else { return }
                            isSaving = true
                            Task {
                                defer { isSaving = false }
                                do {
                                    struct Body: Encodable { let displayName: String }
                                    struct Resp: Decodable { let displayName: String? }
                                    let _: Resp = try await APIClient.shared.put(
                                        path: "/api/settings/profile",
                                        body: Body(displayName: displayName)
                                    )
                                    // Reflect new display name in cached user so other screens update
                                    if let u = authManager.currentUser {
                                        authManager.currentUser = User(id: u.id, email: u.email, displayName: displayName)
                                    }
                                    saveStatus = "Saved!"
                                } catch {
                                    saveStatus = "Save failed"
                                }
                            }
                        }
                        .font(.dmSans(13, weight: .bold))
                        .foregroundColor(isSaving ? .appTextSecondary : .appAccent)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // Email
                SettingRow(label: "Email address", description: "Your login email and where we send notifications") {
                    HStack(spacing: 8) {
                        TextField("Email", text: $email)
                            .font(.dmSans(15))
                            .foregroundColor(.appTextPrimary)
                            .keyboardType(.emailAddress)
                            .autocapitalization(.none)
                            .autocorrectionDisabled()
                            .submitLabel(.done)
                            .onSubmit { hideKeyboard() }

                        Button("Save") {
                            hideKeyboard()
                            // TODO: PUT /api/settings/email { email }
                        }
                        .font(.dmSans(13, weight: .bold))
                        .foregroundColor(.appAccent)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // Phone Number
                SettingRow(label: "Phone number", description: "Optional — used for account recovery") {
                    HStack(spacing: 8) {
                        TextField("+1 555 000 0000", text: $phoneNumber)
                            .font(.dmSans(15))
                            .foregroundColor(.appTextPrimary)
                            .keyboardType(.phonePad)

                        Button("Save") {
                            hideKeyboard()
                            guard !isSaving else { return }
                            isSaving = true
                            Task {
                                defer { isSaving = false }
                                do {
                                    struct Body: Encodable { let phoneNumber: String }
                                    struct Resp: Decodable { let phoneNumber: String? }
                                    let _: Resp = try await APIClient.shared.put(
                                        path: "/api/settings/phone",
                                        body: Body(phoneNumber: phoneNumber)
                                    )
                                    saveStatus = "Saved!"
                                } catch {
                                    saveStatus = "Save failed"
                                }
                            }
                        }
                        .font(.dmSans(13, weight: .bold))
                        .foregroundColor(isSaving ? .appTextSecondary : .appAccent)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // Change Password — opens sheet
                SettingRow(label: "Password", description: "Change your account password") {
                    Button("Change password") {
                        showChangePassword = true
                    }
                    .font(.dmSans(14, weight: .bold))
                    .foregroundColor(.appTextPrimary)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 8)
                    .background(Color.appBackground)
                    .cornerRadius(8)
                    .overlay(
                        RoundedRectangle(cornerRadius: 8)
                            .stroke(Color.appBorder, lineWidth: 1)
                    )
                }
            }
            .background(Color.appCard)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
        }
    }

    // MARK: - Connected Accounts Section
    // Shows real connection status for each provider.
    // Connecting/disconnecting accounts is web-only — OAuth redirects don't return
    // to a native app context, so we show a clear hint instead of a broken button.
    private var connectedAccountsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Connected accounts").sectionHeader()

            // Web-only notice — shown only when at least one account is not connected
            if !spotifyConnected || !googleConnected || !githubConnected {
                HStack(spacing: 8) {
                    Image(systemName: "globe")
                        .font(.system(size: 12))
                        .foregroundColor(.appTextSecondary)
                    Text("To connect accounts, visit Settings on the web app")
                        .font(.dmSans(12))
                        .foregroundColor(.appTextSecondary)
                    Spacer()
                    Link("Open", destination: URL(string: "https://spotifystatistics-production.up.railway.app/Settings")!)
                        .font(.dmSans(12, weight: .bold))
                        .foregroundColor(.appAccent)
                }
                .padding(.horizontal, 4)
            }

            VStack(spacing: 0) {

                // Spotify
                SettingRow(label: "Spotify", description: "Access your listening history and stats") {
                    HStack(spacing: 6) {
                        Circle()
                            .fill(spotifyConnected ? Color.appAccent : Color.red.opacity(0.6))
                            .frame(width: 8, height: 8)
                        Text(spotifyConnected ? "Connected" : "Not connected")
                            .font(.dmSans(13))
                            .foregroundColor(spotifyConnected ? .appAccent : .appTextSecondary)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // Google
                SettingRow(label: "Google", description: "Sign in with your Google account") {
                    HStack(spacing: 6) {
                        Circle()
                            .fill(googleConnected ? Color.appAccent : Color.red.opacity(0.6))
                            .frame(width: 8, height: 8)
                        Text(googleConnected ? "Connected" : "Not connected")
                            .font(.dmSans(13))
                            .foregroundColor(googleConnected ? .appAccent : .appTextSecondary)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // GitHub
                SettingRow(label: "GitHub", description: "Sign in with your GitHub account") {
                    HStack(spacing: 6) {
                        Circle()
                            .fill(githubConnected ? Color.appAccent : Color.red.opacity(0.6))
                            .frame(width: 8, height: 8)
                        Text(githubConnected ? "Connected" : "Not connected")
                            .font(.dmSans(13))
                            .foregroundColor(githubConnected ? .appAccent : .appTextSecondary)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // Sign Out
                Button {
                    authManager.logout()
                } label: {
                    HStack {
                        Text("Sign out")
                            .font(.dmSans(15, weight: .bold))
                            .foregroundColor(.red)
                        Spacer()
                        Image(systemName: "arrow.right.circle")
                            .foregroundColor(.red)
                    }
                    .padding(16)
                }
            }
            .background(Color.appCard)
            .cornerRadius(12)
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.appBorder, lineWidth: 1))
        }
    }

        // MARK: - Danger Zone
    // Delete account — matches web danger zone section
    private var dangerZoneSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Danger zone").sectionHeader()

            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Delete account")
                        .font(.dmSans(15, weight: .bold))
                        .foregroundColor(.appTextPrimary)
                    Text("Permanently delete your account and all data. This cannot be undone.")
                        .font(.dmSans(13))
                        .foregroundColor(.appTextSecondary)
                }

                Spacer()

                Button("Delete") {
                    showDeleteConfirm = true
                }
                .font(.dmSans(13, weight: .bold))
                .foregroundColor(.white)
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.red)
                .cornerRadius(8)
            }
            .padding(16)
            .background(Color.appCard)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.red.opacity(0.3), lineWidth: 1)
            )
        }
    }

    // MARK: - Helpers

    // MARK: OAuth via ASWebAuthenticationSession
    // Opens Google/GitHub login in a secure in-app browser.
    // On success the server redirects to statify://oauth-callback?token=...&email=...
    // which is handled by StatifyiOSApp.onOpenURL → authManager.handleOAuthCallback.
    private func startOAuth(provider: String) {
        Statify.startOAuth(provider: provider, authManager: authManager)
    }

    // Opens a URL in Safari — used for OAuth flows
    private func openURL(_ urlString: String) {
        guard let url = URL(string: urlString) else { return }
        UIApplication.shared.open(url)
    }

    // Dismisses the keyboard by resigning first responder
    private func hideKeyboard() {
        UIApplication.shared.sendAction(
            #selector(UIResponder.resignFirstResponder),
            to: nil, from: nil, for: nil
        )
    }
}

// MARK: - Change Password View
// Shown as a sheet when user taps "Change password"
struct ChangePasswordView: View {

    @Environment(AuthManager.self) private var authManager
    @Environment(\.dismiss) private var dismiss

    @State private var currentPassword: String = ""
    @State private var newPassword:     String = ""
    @State private var confirmPassword: String = ""
    @State private var errors: [String]  = []   // list of validation messages shown below the button
    @State private var isLoading: Bool   = false
    @State private var showSuccess: Bool = false

    // MARK: - Password rules (mirrors ASP.NET Identity defaults)
    // Each rule has a label shown in the checklist and a predicate evaluated live.
    private struct Rule {
        let label: String
        let check: (String) -> Bool
    }

    private let rules: [Rule] = [
        Rule(label: "At least 6 characters")          { $0.count >= 6 },
        Rule(label: "One uppercase letter (A–Z)")      { $0.contains(where: { $0.isUppercase }) },
        Rule(label: "One lowercase letter (a–z)")      { $0.contains(where: { $0.isLowercase }) },
        Rule(label: "One digit (0–9)")                 { $0.contains(where: { $0.isNumber }) },
        Rule(label: "One non-alphanumeric character")  { $0.contains(where: { !$0.isLetter && !$0.isNumber }) },
    ]

    // True only when every rule passes — used to enable the save button
    private var newPasswordValid: Bool { rules.allSatisfy { $0.check(newPassword) } }
    private var passwordsMatch:   Bool { !confirmPassword.isEmpty && newPassword == confirmPassword }

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                ScrollView {
                    VStack(spacing: 20) {

                        // ── Current password ──────────────────────────────────
                        passwordField(
                            label: "Current password",
                            placeholder: "Enter current password",
                            text: $currentPassword,
                            borderColor: .appBorder
                        )

                        // ── New password + live requirements list ─────────────
                        VStack(alignment: .leading, spacing: 8) {
                            passwordField(
                                label: "New password",
                                placeholder: "Enter new password",
                                text: $newPassword,
                                borderColor: newPassword.isEmpty ? .appBorder
                                    : newPasswordValid ? .appAccent : .red.opacity(0.7)
                            )

                            // Requirements checklist — appears once user starts typing
                            if !newPassword.isEmpty {
                                VStack(alignment: .leading, spacing: 5) {
                                    ForEach(rules.indices, id: \.self) { i in
                                        let rule   = rules[i]
                                        let passed = rule.check(newPassword)
                                        HStack(spacing: 6) {
                                            Image(systemName: passed ? "checkmark.circle.fill" : "circle")
                                                .font(.system(size: 13))
                                                .foregroundColor(passed ? .appAccent : .appTextSecondary)
                                            Text(rule.label)
                                                .font(.dmSans(13))
                                                .foregroundColor(passed ? .appAccent : .appTextSecondary)
                                        }
                                    }
                                }
                                .padding(.horizontal, 4)
                                .transition(.opacity)
                                .animation(.easeInOut(duration: 0.2), value: newPassword)
                            }
                        }

                        // ── Confirm new password ──────────────────────────────
                        passwordField(
                            label: "Confirm new password",
                            placeholder: "Repeat new password",
                            text: $confirmPassword,
                            borderColor: confirmPassword.isEmpty ? .appBorder
                                : passwordsMatch ? .appAccent : .red.opacity(0.7)
                        )

                        // Mismatch hint
                        if !confirmPassword.isEmpty && !passwordsMatch {
                            HStack(spacing: 6) {
                                Image(systemName: "exclamationmark.circle")
                                    .font(.system(size: 13))
                                    .foregroundColor(.red)
                                Text("Passwords do not match")
                                    .font(.dmSans(13))
                                    .foregroundColor(.red)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.horizontal, 4)
                        }

                        // ── Server-side errors (returned by API) ──────────────
                        if !errors.isEmpty {
                            VStack(alignment: .leading, spacing: 5) {
                                ForEach(errors, id: \.self) { msg in
                                    HStack(alignment: .top, spacing: 6) {
                                        Image(systemName: "xmark.circle.fill")
                                            .font(.system(size: 13))
                                            .foregroundColor(.red)
                                        Text(msg)
                                            .font(.dmSans(13))
                                            .foregroundColor(.red)
                                    }
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.horizontal, 4)
                        }

                        // ── Save button ───────────────────────────────────────
                        Button {
                            submitPasswordChange()
                        } label: {
                            if isLoading {
                                ProgressView().tint(.black)
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 14)
                            } else {
                                Text("Save password")
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 14)
                            }
                        }
                        .font(.dmSans(16, weight: .bold))
                        .foregroundColor(.black)
                        .background(Color.appAccent)
                        .cornerRadius(10)
                        .disabled(!newPasswordValid || !passwordsMatch || isLoading)
                        .opacity(!newPasswordValid || !passwordsMatch ? 0.45 : 1)
                    }
                    .padding(24)
                    .animation(.easeInOut(duration: 0.2), value: newPassword)
                }
            }
            .navigationTitle("Change Password")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                        .foregroundColor(.appAccent)
                }
            }
            .alert("Password changed!", isPresented: $showSuccess) {
                Button("OK") { dismiss() }
            }
        }
    }

    // MARK: - Submit

    private func submitPasswordChange() {
        errors = []
        isLoading = true

        Task {
            defer { isLoading = false }
            do {
                struct Body: Encodable {
                    let currentPassword: String
                    let newPassword:     String
                }
                // Server returns { ok: true } on success
                struct OkResp: Decodable { let ok: Bool }
                let _: OkResp = try await APIClient.shared.put(
                    path: "/api/settings/password",
                    body: Body(currentPassword: currentPassword, newPassword: newPassword)
                )
                showSuccess = true

            } catch APIError.serverError(let code) where code == 400 {
                // 400 — decode the errors array from the response body.
                // APIClient doesn't expose the raw Data on error, so we re-fetch
                // with a raw request and decode manually.
                await fetchAndShowErrors()

            } catch APIError.serverError(let code) {
                errors = ["Server error (\(code)). Please try again."]
            } catch {
                errors = ["Network error. Check your connection."]
            }
        }
    }

    // Re-fetches the failed response body to extract the errors array.
    // This is needed because APIClient throws before returning data on non-2xx responses.
    private func fetchAndShowErrors() async {
        struct ErrorItem: Decodable { let code: String; let description: String }
        struct ErrorResp: Decodable { let errors: [ErrorItem] }

        struct Body: Encodable {
            let currentPassword: String
            let newPassword:     String
        }

        // Build the request manually to read the 400 body
        guard let url = URL(string: "https://spotifystatistics-production.up.railway.app/api/settings/password") else { return }
        var req = URLRequest(url: url)
        req.httpMethod = "PUT"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if let token = KeychainManager.shared.getToken() {
            req.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        req.httpBody = try? JSONEncoder().encode(Body(currentPassword: currentPassword, newPassword: newPassword))

        guard let (data, _) = try? await URLSession.shared.data(for: req),
              let resp = try? JSONDecoder().decode(ErrorResp.self, from: data) else {
            // Fallback — couldn't decode, show generic messages
            errors = ["Incorrect current password, or new password doesn't meet the requirements."]
            return
        }

        // Map Identity error codes to friendlier messages
        errors = resp.errors.map { friendlyMessage(code: $0.code, description: $0.description) }
    }

    // Translates ASP.NET Identity error codes into plain English hints
    private func friendlyMessage(code: String, description: String) -> String {
        switch code {
        case "PasswordTooShort":
            return "Password must be at least 6 characters."
        case "PasswordRequiresNonAlphanumeric":
            return "Add at least one special character (e.g. !, @, #, $)."
        case "PasswordRequiresDigit":
            return "Add at least one digit (0–9)."
        case "PasswordRequiresLower":
            return "Add at least one lowercase letter (a–z)."
        case "PasswordRequiresUpper":
            return "Add at least one uppercase letter (A–Z)."
        case "PasswordRequiresUniqueChars":
            return "Use more unique characters in your password."
        case "PasswordMismatch":
            return "Current password is incorrect."
        case "UserAlreadyHasPassword":
            return "Account already has a password — use Change password."
        default:
            return description  // fall back to whatever Identity says
        }
    }

    // MARK: - Helpers

    @ViewBuilder
    private func passwordField(label: String, placeholder: String, text: Binding<String>, borderColor: Color) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(label)
                .font(.dmSans(13, weight: .bold))
                .foregroundColor(.appTextSecondary)

            SecureField(placeholder, text: text)
                .font(.dmSans(16))
                .foregroundColor(.appTextPrimary)
                .padding(14)
                .background(Color.appCard)
                .cornerRadius(10)
                .overlay(
                    RoundedRectangle(cornerRadius: 10)
                        .stroke(borderColor, lineWidth: 1)
                )
        }
    }
}

// MARK: - Setting Row
// Generic reusable row used throughout Settings.
// Takes a label, description, and any SwiftUI view as the action area.
struct SettingRow<Action: View>: View {
    let label: String
    let description: String

    // @ViewBuilder allows passing any SwiftUI view as trailing closure
    let action: () -> Action

    init(label: String, description: String, @ViewBuilder action: @escaping () -> Action) {
        self.label = label
        self.description = description
        self.action = action
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            // Row header — label + description
            VStack(alignment: .leading, spacing: 2) {
                Text(label)
                    .font(.dmSans(15, weight: .bold))
                    .foregroundColor(.appTextPrimary)
                Text(description)
                    .font(.dmSans(13))
                    .foregroundColor(.appTextSecondary)
            }
            // Action area — text field, button, etc.
            action()
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}
