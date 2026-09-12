//
//  ChatViewModel.swift
//  StatifyiOS
//

import Foundation

@Observable
@MainActor
final class ChatViewModel {

    private(set) var messages: [ChatMessage] = [
        ChatMessage(
            role: .bot,
            text: "Hi! Ask about your top artists, recent tracks, Spotify status, or listening stats."
        )
    ]

    var draft: String = ""
    private(set) var isSending: Bool = false
    private(set) var errorMessage: String?

    func send() async {
        let text = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty, !isSending else { return }

        draft = ""
        errorMessage = nil
        isSending = true

        messages.append(ChatMessage(role: .user, text: text))
        let pendingId = UUID()
        messages.append(ChatMessage(id: pendingId, role: .pending, text: "Thinking…"))

        do {
            let response: ChatResponse = try await APIClient.shared.post(
                path: "/api/chat",
                body: ChatRequest(message: text)
            )
            messages.removeAll { $0.id == pendingId }
            messages.append(ChatMessage(role: .bot, text: response.reply))
        } catch APIError.unauthorized {
            messages.removeAll { $0.id == pendingId }
            errorMessage = "Session expired — please sign in again."
            messages.append(ChatMessage(role: .bot, text: errorMessage!))
        } catch {
            messages.removeAll { $0.id == pendingId }
            errorMessage = "Couldn't reach Ask Statify. Check your connection."
            messages.append(ChatMessage(role: .bot, text: errorMessage!))
        }

        isSending = false
    }
}
