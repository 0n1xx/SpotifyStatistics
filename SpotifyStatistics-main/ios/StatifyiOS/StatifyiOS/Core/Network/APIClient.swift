//
//  APIClient.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import Foundation

// MARK: - API Error
// All possible errors that APIClient can return.
// enum is ideal here because the number of error types is fixed and each one has a specific, well-defined meaning.
enum APIError: Error {
    case invalidURL          // Malformed or missing URL
    case noData              // Server returned an empty response
    case decodingFailed      // Failed to parse JSON into a model
    case unauthorized        // 401 — token expired or invalid
    case serverError(Int)    // 500+ — server-side error with status code
    case unknown(Error)      // Any other unexpected error
}

// MARK: - HTTP Method
// Enum of supported HTTP methods.
// ': String' gives each case a raw string value equal to its name,
// so HTTPMethod.GET.rawValue returns "GET" — needed by URLRequest.
enum HTTPMethod: String {
    case GET
    case POST
    case PUT
    case DELETE
}

// MARK: - APIClient
// Central class for all network requests in the app.
// Every screen goes through this — no one creates URLSession directly.
// Implemented as a Singleton via 'static let shared'.
final class APIClient {

    // The single shared instance of APIClient.
    // Access it anywhere with APIClient.shared.get(...)
    static let shared = APIClient()

    // Base URL of the ASP.NET backend deployed on Railway.
    // Change it here and it updates everywhere.
    private let baseURL = "https://spotifystatistics-production.up.railway.app"

    // URLSession is iOS's built-in HTTP client.
    // .shared is the default configuration — suitable for most use cases.
    private let session = URLSession.shared

    // Private init enforces the Singleton pattern.
    // Prevents anyone from calling APIClient() directly.
    // The only way to use this class is through APIClient.shared.
    private init() {}

    // MARK: - Request Builder
    // Private helper that assembles a URLRequest from our parameters.
    // All public methods (get, post, etc.) call this internally.
    private func buildRequest(
        path: String,           // e.g. "/api/dashboard"
        method: HTTPMethod,     // GET, POST, PUT, DELETE
        body: Data? = nil       // JSON body for POST/PUT requests
    ) throws -> URLRequest {

        // Combine base URL with the path to form the full URL
        guard let url = URL(string: baseURL + path) else {
            throw APIError.invalidURL
        }

        var request = URLRequest(url: url)
        request.httpMethod = method.rawValue

        // Tell the server we're sending and expecting JSON
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        // If the user is logged in, attach their JWT token to every request.
        // Bearer is the standard authorization scheme for REST APIs.
        if let token = KeychainManager.shared.getToken() {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }

        // Attach the request body if provided (POST/PUT only)
        request.httpBody = body

        return request
    }

    // MARK: - Generic Request
    // Core method that executes the request and decodes the response.
    // T: Decodable — T is any type that can be decoded from JSON.
    // async throws — non-blocking (won't freeze the UI) and can throw errors.
    func request<T: Decodable>(
        path: String,
        method: HTTPMethod = .GET,
        body: Data? = nil
    ) async throws -> T {

        // Build the request — throws if the URL is invalid
        let urlRequest = try buildRequest(path: path, method: method, body: body)

        // Suspend here and wait for the server response without blocking the UI thread.
        // session.data returns a tuple: (Data, URLResponse)
        let (data, response) = try await session.data(for: urlRequest)

        // Inspect the HTTP status code
        if let httpResponse = response as? HTTPURLResponse {
            // DEBUG — remove before release
            print("🌐 [\(httpResponse.statusCode)] \(urlRequest.url?.path ?? "")")
            if let raw = String(data: data, encoding: .utf8) {
                print("📦 RAW JSON: \(raw.prefix(500))")
            }

            switch httpResponse.statusCode {
            case 200...299:
                // Success — continue to decoding
                break
            case 401:
                // Token expired or invalid — caller should log the user out
                throw APIError.unauthorized
            default:
                throw APIError.serverError(httpResponse.statusCode)
            }
        }

        // Decode the raw JSON bytes into our Swift model.
        // No keyDecodingStrategy — C# System.Text.Json outputs camelCase by default.
        do {
            let decoder = JSONDecoder()
            return try decoder.decode(T.self, from: data)
        } catch {
            // DEBUG — remove before release
            print("❌ DECODE ERROR for \(T.self): \(error)")
            throw APIError.decodingFailed
        }
    }

    // MARK: - Convenience Methods
    // Shortcuts so callers don't need to pass method: .GET explicitly

    // GET — fetch data from the server
    func get<T: Decodable>(path: String) async throws -> T {
        try await request(path: path, method: .GET)
    }

    // POST — send data to the server (login, register, etc.)
    // B: Encodable — the body must be serializable to JSON
    func post<T: Decodable, B: Encodable>(path: String, body: B) async throws -> T {
        let data = try JSONEncoder().encode(body)
        return try await request(path: path, method: .POST, body: data)
    }

    // PUT — update existing data (profile, password, etc.)
    func put<T: Decodable, B: Encodable>(path: String, body: B) async throws -> T {
        let data = try JSONEncoder().encode(body)
        return try await request(path: path, method: .PUT, body: data)
    }

    // DELETE — remove a resource (account deletion, etc.)
    func delete(path: String) async throws {
        let _: EmptyResponse = try await request(path: path, method: .DELETE)
    }
}

// MARK: - Empty Response
// Some endpoints return no body (e.g. DELETE).
// We need an empty Decodable struct so the decoder doesn't crash.
struct EmptyResponse: Decodable {}
