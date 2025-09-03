import SwiftUI
import SwiftData

struct AnalysisView: View {
    @Environment(\.modelContext) private var context
    @Query(sort: [\.date]) private var workouts: [Workout]
    @State private var formula: OneRMFormula = .epley

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: 16) {
                Picker("1RM Formula", selection: $formula) {
                    Text("Epley").tag(OneRMFormula.epley)
                    Text("Brzycki").tag(OneRMFormula.brzycki)
                }
                .pickerStyle(.segmented)

                let stats = workouts.map { AnalyticsService.stats(for: $0, formula: formula) }
                let totalVolume = stats.reduce(0) { $0 + $1.totalVolumeKg }
                let best1RM = stats.map { $0.best1RMKg }.max() ?? 0

                GroupBox("Overview") {
                    VStack(alignment: .leading) {
                        Text("Workouts: \(workouts.count)")
                        Text("Total Volume: \(totalVolume, specifier: "%.0f") kg")
                        Text("Best 1RM: \(best1RM, specifier: "%.1f") kg")
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                Spacer()
            }
            .padding()
            .navigationTitle("Analysis")
        }
    }
}

#Preview { AnalysisView() }

