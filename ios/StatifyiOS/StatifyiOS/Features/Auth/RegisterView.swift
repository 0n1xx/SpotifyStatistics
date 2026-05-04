//
//  RegisterView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct RegisterView: View {

    @Environment(AuthManager.self) private var authManager
    @Environment(\.dismiss) private var dismiss

    @State private var email: String = ""
    @State private var password: String = ""
    @State private var confirmPassword: String = ""
    @State private var showPassword: Bool = false

    // Local validation error — different from authManager.errorMessage
    // This catches mismatched passwords before hitting the network
    @State private var validationError: String? = nil

    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()

            ScrollView {
                VStack(spacing: 32) {

                    // MARK: Header
                    VStack(spacing: 8) {
                        Text("Create Account")
                            .font(.syne(32, weight: .bold))
                            .foregroundColor(.appTextPrimary)

                        Text("Start tracking your music")
                            .font(.dmSans(16))
                            .foregroundColor(.appTextSecondary)
                    }
                    .padding(.top, 60)

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

                        // Confirm password field
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Confirm Password")
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
                                            // Red border if passwords don't match
                                            confirmPassword.isEmpty ? Color.appBorder :
                                            password == confirmPassword ? Color.appAccent : Color.red,
                                            lineWidth: 1
                                        )
                                )
                        }

                        // MARK: Error Messages
                        // Show local validation error or network error
                        if let error = validationError ?? authManager.errorMessage {
                            Text(error)
                                .font(.dmSans(14))
                                .foregroundColor(.red)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }

                        // MARK: Register Button
                        Button {
                            // Validate locally before hitting the network
                            guard password == confirmPassword else {
                                validationError = "Passwords do not match."
                                return
                            }
                            guard password.count >= 6 else {
                                validationError = "Password must be at least 6 characters."
                                return
                            }

                            validationError = nil

                            Task {
                                await authManager.register(
                                    email: email,
                                    password: password
                                )
                                // If registration succeeded — dismiss the sheet
                                if authManager.isLoggedIn {
                                    dismiss()
                                }
                            }
                        } label: {
                            if authManager.isLoading {
                                ProgressView()
                                    .tint(.black)
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 14)
                            } else {
                                Text("Create Account")
                                    .frame(maxWidth: .infinity)
                                    .padding(.vertical, 14)
                            }
                        }
                        .font(.dmSans(16, weight: .bold))
                        .foregroundColor(.black)
                        .background(Color.appAccent)
                        .cornerRadius(10)
                        .disabled(authManager.isLoading || email.isEmpty || password.isEmpty)
                        .opacity(email.isEmpty || password.isEmpty ? 0.5 : 1)
                    }

                    // MARK: Back to Login
                    Button {
                        dismiss()
                    } label: {
                        HStack(spacing: 4) {
                            Text("Already have an account?")
                                .foregroundColor(.appTextSecondary)
                            Text("Log in")
                                .foregroundColor(.appAccent)
                        }
                        .font(.dmSans(15))
                    }

                    Spacer()
                }
                .padding(.horizontal, 24)
            }
        }
    }
}
