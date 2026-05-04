//
//  RecentlyPlayedView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct RecentlyPlayedView: View {
    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()
            Text("Recently Played")
                .font(.syne(24, weight: .bold))
                .foregroundColor(.appTextPrimary)
        }
    }
}
