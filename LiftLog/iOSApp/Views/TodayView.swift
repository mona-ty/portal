import SwiftUI
import SwiftData

struct TodayView: View {
    @Environment(\.modelContext) private var context
    @EnvironmentObject private var session: WorkoutSessionViewModel
    @Query(sort: [\.name]) private var exercises: [Exercise]
    @State private var selectedUnit: UnitSystem = .kg

    var body: some View {
        NavigationStack {
            VStack(spacing: 16) {
                Picker("Units", selection: $selectedUnit) {
                    Text("kg").tag(UnitSystem.kg)
                    Text("lb").tag(UnitSystem.lb)
                }
                .pickerStyle(.segmented)

                if session.activeWorkout == nil {
                    Button {
                        session.startNewWorkout(context: context, unit: selectedUnit)
                    } label: {
                        Label("Start Workout", systemImage: "play.fill")
                            .font(.title2)
                            .padding(.vertical, 8)
                    }
                    .buttonStyle(.borderedProminent)
                    NavigationLink("Start from Template") {
                        TemplatePickerView(unit: selectedUnit)
                            .environmentObject(session)
                    }
                } else {
                    Text("Workout in progress")
                        .font(.headline)
                    NavigationLink("Go to Record", destination: RecordView().environmentObject(session))
                }

                Divider()
                if exercises.isEmpty {
                    Text("Add exercises in Settings → Exercises")
                        .foregroundStyle(.secondary)
                } else {
                    List(exercises.prefix(5)) { ex in
                        Text(ex.name)
                    }
                    .frame(maxHeight: 200)
                }
                Spacer()
            }
            .padding()
            .navigationTitle("Today")
            .task { await NotificationService.requestAuthorization() }
        }
    }
}

#Preview { TodayView().environmentObject(WorkoutSessionViewModel()) }
