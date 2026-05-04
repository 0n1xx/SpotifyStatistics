//
//  DashboardView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct DashboardView: View {
    @Environment(AuthManager.self) private var authManager

    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()
            Text("Dashboard")
                .font(.syne(24, weight: .bold))
                .foregroundColor(.appTextPrimary)
        }
    }
}
