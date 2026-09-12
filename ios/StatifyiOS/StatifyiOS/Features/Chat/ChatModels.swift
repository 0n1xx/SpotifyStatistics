//
//  ChatModels.swift
//  StatifyiOS
//

import Foundation

// MARK: - Chat Request / Response
// Maps to POST /api/chat  { message } → { reply }

struct ChatRequest: Encodable {
    let message: String
}

struct ChatResponse: Decodable {
    let reply: String
}

// MARK: - Chat Message (local UI model)
struct ChatMessage: Identifiable, Equatable {
    enum Role: Equatable {
        case user
        case bot
        case pending
    }

    let id: UUID
    let role: Role
    let text: String
    let createdAt: Date

    init(id: UUID = UUID(), role: Role, text: String, createdAt: Date = Date()) {
        self.id = id
        self.role = role
        self.text = text
        self.createdAt = createdAt
    }
}
