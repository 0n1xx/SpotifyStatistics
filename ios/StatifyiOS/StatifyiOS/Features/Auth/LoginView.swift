//
//  LoginView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct LoginView: View {

    // MARK: - Environment
    // AuthManager comes from ContentView via .environment()
    // We don't create it here — we just read it from the environment
    @Environment(AuthManager.self) private var authManager

    // MARK: - Local State
    // @State is for simple values that belong to this view only.
    // When these change → SwiftUI re-renders the view automatically.
    @State private var email: String = ""
    @State private var password: String = ""

    // Controls whether we show LoginView or RegisterView
    @State private var showRegister: Bool = false

    // Controls whether password field shows plain text or dots
    @State private var showPassword: Bool = false

    // MARK: - Body
    var body: some View {
        ZStack {
            // Full screen dark background
            Color.appBackground.ignoresSafeArea()

            ScrollView {
                VStack(spacing: 32) {

                    // MARK: Header
                    VStack(spacing: 8) {
                        Text("Statify")
                            .font(.syne(40, weight: .bold))
                            .foregroundColor(.appAccent)

                        Text("Your music. Your stats.")
                            .font(.dmSans(16))
                            .foregroundColor(.appTextSecondary)
                    }
                    .padding(.top, 80)

                    // MARK: Form
                    VStack(spacing: 16) {

                        // Email field
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Email")
                                .font(.dmSans(13, weight: .bold))
                                .foregroundColor(.appTextSecondary)

                            TextField("you@example.com", text: $email)
                                .font(.dmSans(16))
                                .foregroundColor(.appTextPrimary)
                                .keyboardType(.emailAddress)
                                .autocapitalization(.none)
                                .autocorrectionDisabled()
                                .padding(14)
                                .background(Color.appCard)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(Color.appBorder, lineWidth: 1)
                                )
                        }

                        // Password field
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Password")
                                .font(.dmSans(13, weight: .bold))
                                .foregroundColor(.appTextSecondary)

                            HStack {
                                // Switch between secure and plain text
                                if showPassword {
                                    TextField("••••••••", text: $password)
                                        .font(.dmSans(16))
                                        .foregroundColor(.appTextPrimary)
                                        .autocapitalization(.none)
                                        .autocorrectionDisabled()
                                } else {
                                    SecureField("••••••••", text: $password)
                                        .font(.dmSans(16))
                                        .foregroundColor(.appTextPrimary)
                                }

                                // Toggle show/hide password
                                Button {
                                    showPassword.toggle()
                                } label: {
                                    Image(systemName: showPassword ? "eye.slash" : "eye")
                                        .foregroundColor(.appTextSecondary)
                                }
                            }
                            .padding(14)
                            .background(Color.appCard)
                            .cornerRadius(10)
                            .overlay(
                                RoundedRectangle(cornerRadius: 10)
                                    .stroke(Color.appBorder, lineWidth: 1)
                            )
                        }

                        // MARK: Error Message
                        // Only visible when authManager.errorMessage is not nil
                        if let error = authManager.errorMessage {
                            Text(error)
                                .font(.dmSans(14))
                                .foregroundColor(.red)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        // MARK: Login Button
                        Button {
                            Task {
                                // Task wraps async call inside a sync context
                                // SwiftUI button actions are not async by default
                                await authManager.login(
                                    email: email,
                                    password: password
                                )
                            }
                        } label: {
                            // Show spinner when loading, text otherwise
                            if authManager.isLoading {
                                ProgressView()
                                    .tint(.black)
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 14)
                            } else {
                                Text("Log In")
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 14)
                            }
                        }
                        .font(.dmSans(16, weight: .bold))
                        .foregroundColor(.black)
                        .background(Color.appAccent)
                        .cornerRadius(10)
                        // Disable button while loading or fields are empty
                        .disabled(authManager.isLoading || email.isEmpty || password.isEmpty)
                        .opacity(email.isEmpty || password.isEmpty ? 0.5 : 1)
                    }

                    // MARK: Register Link
                    Button {
                        showRegister = true
                    } label: {
                        HStack(spacing: 4) {
                            Text("Don't have an account?")
                                .foregroundColor(.appTextSecondary)
                            Text("Sign up")
                                .foregroundColor(.appAccent)
                        }
                        .font(.dmSans(15))
                    }

                    Spacer()
                }
                .padding(.horizontal, 24)
            }
        }
        // Navigate to RegisterView when showRegister is true
        .sheet(isPresented: $showRegister) {
            RegisterView()
                .environment(authManager)
        }
    }
}
