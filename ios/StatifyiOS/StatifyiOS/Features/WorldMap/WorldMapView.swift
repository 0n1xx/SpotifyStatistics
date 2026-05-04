//
//  WorldMapView.swift
//  StatifyiOS
//
//  Created by Vlad Sakharov on 2026-05-04.
//

import SwiftUI

struct WorldMapView: View {
    var body: some View {
        ZStack {
            Color.appBackground.ignoresSafeArea()
            Text("World Map")
                .font(.syne(24, weight: .bold))
                .foregroundColor(.appTextPrimary)
        }
    }
}
