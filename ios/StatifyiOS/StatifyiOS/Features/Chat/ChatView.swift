//
//  ChatView.swift
//  StatifyiOS
//

import SwiftUI

struct ChatView: View {

    @State private var viewModel = ChatViewModel()
    @FocusState private var inputFocused: Bool

    var body: some View {
        NavigationStack {
            ZStack {
                Color.appBackground.ignoresSafeArea()

                VStack(spacing: 0) {
                    messagesList
                    Divider().overlay(Color.appBorder)
                    composer
                }
            }
            .navigationTitle("Ask Statify")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItemGroup(placement: .keyboard) {
                    Spacer()
                    Button("Done") {
                        dismissKeyboard()
                    }
                    .font(.dmSans(16, weight: .bold))
                    .foregroundColor(.appAccent)
                }
            }
        }
    }

    // MARK: - Messages

    private var messagesList: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 12) {
                    ForEach(viewModel.messages) { message in
                        bubble(for: message)
                            .id(message.id)
                    }
                }
                .padding(16)
                .frame(maxWidth: .infinity, minHeight: 0, alignment: .topLeading)
                // Extra empty space so tapping below the last bubble also dismisses
                .background(Color.appBackground)
                .contentShape(Rectangle())
                .onTapGesture { dismissKeyboard() }
            }
            .scrollDismissesKeyboard(.interactively)
            .onChange(of: viewModel.messages.count) { _, _ in
                if let last = viewModel.messages.last {
                    withAnimation(.easeOut(duration: 0.2)) {
                        proxy.scrollTo(last.id, anchor: .bottom)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func bubble(for message: ChatMessage) -> some View {
        HStack {
            if message.role == .user { Spacer(minLength: 48) }

            Text(message.text)
                .font(.dmSans(15))
                .foregroundColor(message.role == .user ? .appTextPrimary : .appTextPrimary)
                .opacity(message.role == .pending ? 0.7 : 1)
                .italic(message.role == .pending)
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(bubbleBackground(for: message.role))
                .overlay(
                    RoundedRectangle(cornerRadius: 14)
                        .stroke(bubbleBorder(for: message.role), lineWidth: 1)
                )
                .cornerRadius(14)
                .frame(maxWidth: 300, alignment: message.role == .user ? .trailing : .leading)

            if message.role != .user { Spacer(minLength: 48) }
        }
        .contentShape(Rectangle())
        .onTapGesture { dismissKeyboard() }
    }

    private func bubbleBackground(for role: ChatMessage.Role) -> Color {
        switch role {
        case .user:
            return Color.appAccent.opacity(0.18)
        case .bot, .pending:
            return Color.appCard
        }
    }

    private func bubbleBorder(for role: ChatMessage.Role) -> Color {
        switch role {
        case .user:
            return Color.appAccent.opacity(0.3)
        case .bot, .pending:
            return Color.appBorder
        }
    }

    // MARK: - Composer

    private var composer: some View {
        HStack(spacing: 10) {
            TextField("Ask something…", text: $viewModel.draft, axis: .vertical)
                .font(.dmSans(16))
                .foregroundColor(.appTextPrimary)
                .lineLimit(1...4)
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(Color.appCard)
                .cornerRadius(12)
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(Color.appBorder, lineWidth: 1)
                )
                .focused($inputFocused)
                .submitLabel(.send)
                .onSubmit {
                    dismissKeyboard()
                    Task { await viewModel.send() }
                }

            Button {
                dismissKeyboard()
                Task { await viewModel.send() }
            } label: {
                Image(systemName: "arrow.up.circle.fill")
                    .font(.system(size: 32))
                    .foregroundColor(canSend ? .appAccent : .appTextSecondary)
            }
            .disabled(!canSend)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .background(Color.appBackground)
    }

    private var canSend: Bool {
        !viewModel.isSending &&
        !viewModel.draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private func dismissKeyboard() {
        inputFocused = false
        UIApplication.shared.sendAction(
            #selector(UIResponder.resignFirstResponder),
            to: nil, from: nil, for: nil
        )
    }
}
