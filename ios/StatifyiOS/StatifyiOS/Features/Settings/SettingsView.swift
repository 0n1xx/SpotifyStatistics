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
import SafariServices   // Required for opening OAuth URLs in Safari

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
    @State private var showOAuthAlert: Bool = false         // Web-only OAuth info alert
    @State private var oAuthProvider: String = ""           // Provider name shown in alert

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
        // Populate fields from current user on appear
        .onAppear {
            email = authManager.currentUser?.email ?? ""
            displayName = authManager.currentUser?.displayName ?? ""
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
        // MARK: Web-only OAuth Info Alert
        // Google and GitHub OAuth run through the web browser session, not the app.
        // Connecting them on the website keeps both platforms in sync automatically.
        .alert("Connect \(oAuthProvider) on the web", isPresented: $showOAuthAlert) {
            Button("Open website") {
                openURL("https://spotifystatistics-production.up.railway.app/Identity/Account/Manage/ExternalLogins")
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("To link your \(oAuthProvider) account, visit Settings on the website. Once connected there, it will be active here too.")
        }
        // MARK: Photo Selection Handler
        // Triggered when user picks a photo from the library
        .onChange(of: selectedPhoto) {
            Task {
                if let data = try? await selectedPhoto?.loadTransferable(type: Data.self),
                   let image = UIImage(data: data) {
                    avatarImage = image
                    // TODO: Upload to PUT /api/settings/avatar
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

                    // Upload photo button — same as web
                    Text("Upload photo")
                        .font(.dmSans(13))
                        .foregroundColor(.appAccent)
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
    // Spotify, Google, GitHub — matches web Connected accounts section
    private var connectedAccountsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Connected accounts").sectionHeader()

            VStack(spacing: 0) {

                // Spotify
                SettingRow(label: "Spotify", description: "Access your listening history and stats") {
                    HStack(spacing: 8) {
                        // Status dot
                        Circle()
                            .fill(Color.red.opacity(0.6))
                            .frame(width: 8, height: 8)

                        Text("Not connected")
                            .font(.dmSans(13))
                            .foregroundColor(.appTextSecondary)

                        Spacer()

                        // Opens Spotify OAuth in Safari
                        Button("Connect") {
                            openURL("https://spotifystatistics-production.up.railway.app/SpotifyAuth/login")
                        }
                        .font(.dmSans(13, weight: .bold))
                        .foregroundColor(.black)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Color.appAccent)
                        .cornerRadius(8)
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // Google — web-only OAuth, show info only
                SettingRow(label: "Google", description: "Sign in with your Google account") {
                    HStack(spacing: 8) {
                        Circle()
                            .fill(Color.red.opacity(0.6))
                            .frame(width: 8, height: 8)
                        Text("Not connected")
                            .font(.dmSans(13))
                            .foregroundColor(.appTextSecondary)

                        Spacer()

                        Button("Connect") {
                            oAuthProvider = "Google"
                            showOAuthAlert = true
                        }
                        .font(.dmSans(13, weight: .bold))
                        .foregroundColor(.appTextPrimary)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Color.appBackground)
                        .cornerRadius(8)
                        .overlay(
                            RoundedRectangle(cornerRadius: 8)
                                .stroke(Color.appBorder, lineWidth: 1)
                        )
                    }
                }

                Divider().background(Color.appBorder).padding(.horizontal, 16)

                // GitHub
                SettingRow(label: "GitHub", description: "Sign in with your GitHub account") {
                    HStack(spacing: 8) {
                        Circle()
                            .fill(Color.red.opacity(0.6))
                            .frame(width: 8, height: 8)
                        Text("Not connected")
                            .font(.dmSans(13))
                            .foregroundColor(.appTextSecondary)

                        Spacer()

                        Button("Connect") {
                            oAuthProvider = "GitHub"
                            showOAuthAlert = true
                        }
                        .font(.dmSans(13, weight: .bold))
                        .foregroundColor(.appTextPrimary)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Color.appBackground)
                        .cornerRadius(8)
                        .overlay(
                            RoundedRectangle(cornerRadius: 8)
                                .stroke(Color.appBorder, lineWidth: 1)
                        )
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
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.appBorder, lineWidth: 1)
            )
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
    @State private var newPassword: String = ""
    @State private var confirmPassword: String = ""
    @State private var errorMessage: String? = nil
    @State private var isLoading: Bool = false
    @State private var showSuccess: Bool = false

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                ScrollView {
                    VStack(spacing: 20) {

                        // Current password
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Current password")
                                .font(.dmSans(13, weight: .bold))
                                .foregroundColor(.appTextSecondary)

                            SecureField("••••••••", text: $currentPassword)
                                .font(.dmSans(16))
                                .foregroundColor(.appTextPrimary)
                                .padding(14)
                                .background(Color.appCard)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(Color.appBorder, lineWidth: 1)
                                )
                        }

                        // New password
                        VStack(alignment: .leading, spacing: 6) {
                            Text("New password")
                                .font(.dmSans(13, weight: .bold))
                                .foregroundColor(.appTextSecondary)

                            SecureField("••••••••", text: $newPassword)
                                .font(.dmSans(16))
                                .foregroundColor(.appTextPrimary)
                                .padding(14)
                                .background(Color.appCard)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(Color.appBorder, lineWidth: 1)
                                )
                        }

                        // Confirm new password
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Confirm new password")
                                .font(.dmSans(13, weight: .bold))
                                .foregroundColor(.appTextSecondary)

                            SecureField("••••••••", text: $confirmPassword)
                                .font(.dmSans(16))
                                .foregroundColor(.appTextPrimary)
                                .padding(14)
                                .background(Color.appCard)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(
                                            confirmPassword.isEmpty ? Color.appBorder :
                                            newPassword == confirmPassword ? Color.appAccent : Color.red,
                                            lineWidth: 1
                                        )
                                )
                        }

                        // Error message
                        if let error = errorMessage {
                            Text(error)
                                .font(.dmSans(14))
                                .foregroundColor(.red)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        // Save button
                        Button {
                            guard newPassword == confirmPassword else {
                                errorMessage = "Passwords do not match."
                                return
                            }
                            guard newPassword.count >= 6 else {
                                errorMessage = "Password must be at least 6 characters."
                                return
                            }
                            // TODO: PUT /api/settings/password
                            showSuccess = true
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
                        .disabled(currentPassword.isEmpty || newPassword.isEmpty || confirmPassword.isEmpty)
                        .opacity(currentPassword.isEmpty || newPassword.isEmpty ? 0.5 : 1)
                    }
                    .padding(24)
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
