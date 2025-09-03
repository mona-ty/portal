import SwiftUI

struct MainTabView: View {
    @StateObject private var sessionVM = WorkoutSessionViewModel()

    var body: some View {
        TabView {
            TodayView()
                .environmentObject(sessionVM)
                .tabItem { Label("Today", systemImage: "bolt.fill") }

            RecordView()
                .environmentObject(sessionVM)
                .tabItem { Label("Record", systemImage: "pencil") }

            HistoryView()
                .tabItem { Label("History", systemImage: "clock") }

            AnalysisView()
                .tabItem { Label("Analysis", systemImage: "chart.bar.xaxis") }

            SettingsView()
                .tabItem { Label("Settings", systemImage: "gear") }
        }
    }
}

#Preview {
    MainTabView()
}

