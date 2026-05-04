//
//  ContentView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-03.
//

import SwiftUI

struct ContentView: View {
    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()
            Text("Statify")
                .font(.syne(32, weight: .bold))
                .foregroundColor(.appAccent)
        }
    }
}

#Preview {
    ContentView()
}
