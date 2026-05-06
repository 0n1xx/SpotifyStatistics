//
//  PlainTextField.swift
//  StatifyiOS
//

import SwiftUI
import UIKit

// MARK: - PlainTextField
// UIViewRepresentable wrapper that bypasses SwiftUI's autofill tinting.
// SwiftUI TextField turns blue when iOS Password AutoFill activates — this prevents it.
struct PlainTextField: UIViewRepresentable {
    let placeholder: String
    @Binding var text: String
    var keyboardType: UIKeyboardType = .default

    func makeUIView(context: Context) -> UITextField {
        let tf = UITextField()
        tf.placeholder = placeholder
        tf.keyboardType = keyboardType
        tf.autocapitalizationType = .none
        tf.autocorrectionType = .no
        tf.textContentType = .oneTimeCode  // Disables autofill highlight
        tf.tintColor = UIColor(named: "AppTextSecondary") ?? .gray
        tf.textColor = UIColor(named: "AppTextSecondary") ?? .gray
        tf.font = UIFont.systemFont(ofSize: 16)
        tf.delegate = context.coordinator
        tf.addTarget(context.coordinator, action: #selector(Coordinator.textChanged(_:)), for: .editingChanged)
        return tf
    }

    func updateUIView(_ uiView: UITextField, context: Context) {
        if uiView.text != text { uiView.text = text }
    }

    func makeCoordinator() -> Coordinator { Coordinator(text: $text) }

    class Coordinator: NSObject, UITextFieldDelegate {
        @Binding var text: String
        init(text: Binding<String>) { _text = text }
        @objc func textChanged(_ tf: UITextField) { text = tf.text ?? "" }
    }
}
