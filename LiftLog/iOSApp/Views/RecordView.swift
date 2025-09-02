import SwiftUI
import SwiftData

struct RecordView: View {
    @Environment(\.modelContext) private var context
    @EnvironmentObject private var session: WorkoutSessionViewModel
    @Query(sort: [\.name]) private var exercises: [Exercise]
    @State private var selectedExercise: Exercise?
    @State private var displayWeight: Double = 60
    @State private var reps: Int = 5
    @State private var rpe: Double = 8.0
    @State private var isWarmup: Bool = false
    @State private var restSec: Int = 120

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: 16) {
                if session.activeWorkout == nil {
                    Text("No active workout").foregroundStyle(.secondary)
                    NavigationLink("Start from Today", destination: TodayView().environmentObject(session))
                    Spacer()
                } else {
                    if let t = session.activeTemplate {
                        GroupBox("Template") {
                            VStack(alignment: .leading, spacing: 6) {
                                Text(t.name).font(.headline)
                                ForEach(t.items) { item in
                                    HStack {
                                        Text(item.exercise.name)
                                        Spacer()
                                        Text("\(item.targetSets)x\(item.targetReps)")
                                    }
                                }
                            }
                        }
                    }
                    Picker("Exercise", selection: $selectedExercise) {
                        ForEach(exercises) { ex in Text(ex.name).tag(Optional(ex)) }
                    }
                    .pickerStyle(.navigationLink)

                    HStack {
                        Stepper(value: $displayWeight, in: 0...500, step: 2.5) {
                            Text("Weight: \(displayWeight, specifier: "%.1f")")
                        }
                        .fixedSize()
                        Spacer()
                        Stepper(value: $reps, in: 1...30) { Text("Reps: \(reps)") }
                    }

                    HStack {
                        Slider(value: $rpe, in: 6...10, step: 0.5) { Text("RPE") }
                        Text(rpe, format: .number.precision(.fractionLength(1)))
                            .frame(width: 44)
                    }
                    Toggle("Warm-up set", isOn: $isWarmup)

                    HStack {
                        Stepper(value: $restSec, in: 30...600, step: 15) { Text("Rest: \(restSec)s") }
                        if session.isResting {
                            Text("⏱ \(session.secondsRemaining)s")
                        }
                    }

                    Button {
                        if let ex = selectedExercise, let w = session.activeWorkout {
                            session.addSet(context: context, exercise: ex, displayWeight: displayWeight, unit: w.unit, reps: reps, rpe: rpe, isWarmup: isWarmup, restSec: restSec)
                        }
                    } label: {
                        Label("Add Set", systemImage: "plus")
                    }
                    .buttonStyle(.borderedProminent)

                    if let workout = session.activeWorkout {
                        List(workout.sets) { set in
                            HStack {
                                Text(set.exercise.name)
                                Spacer()
                                Text("\(set.reps)x @ \(UnitService.toDisplayWeight(set.weight, unit: workout.unit), specifier: "%.1f")")
                            }
                        }
                        .frame(maxHeight: 250)
                    }

                    HStack {
                        Button(role: .destructive) { session.endWorkout() } label: { Text("End Workout") }
                        Spacer()
                        Button { if restSec > 0 { session.startRest(seconds: restSec) } } label: { Text("Start Rest") }
                    }
                }
                Spacer()
            }
            .padding()
            .navigationTitle("Record")
        }
    }
}

#Preview { RecordView().environmentObject(WorkoutSessionViewModel()) }
