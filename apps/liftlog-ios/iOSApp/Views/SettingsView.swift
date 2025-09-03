import SwiftUI
import SwiftData

struct SettingsView: View {
    @StateObject private var vm = SettingsViewModel()
    @Environment(\.modelContext) private var context
    @State private var hkAuthorized: Bool = false

    var body: some View {
        NavigationStack {
            Form {
                Section("General") {
                    Picker("Units", selection: Binding(get: { vm.unit }, set: { vm.unit = $0 })) {
                        Text("kg").tag(UnitSystem.kg)
                        Text("lb").tag(UnitSystem.lb)
                    }
                    Picker("1RM Formula", selection: Binding(get: { vm.oneRMFormula }, set: { vm.oneRMFormula = $0 })) {
                        Text("Epley").tag(OneRMFormula.epley)
                        Text("Brzycki").tag(OneRMFormula.brzycki)
                    }
                }

                Section("Health") {
                    HStack {
                        Text("HealthKit")
                        Spacer()
                        Image(systemName: hkAuthorized ? "checkmark.seal.fill" : "exclamationmark.triangle.fill")
                            .foregroundStyle(hkAuthorized ? .green : .orange)
                    }
                    Button("Request Health Permissions") {
                        Task {
                            do { try await HealthKitService.shared.requestAuthorization(); hkAuthorized = true } catch { hkAuthorized = false }
                        }
                    }
                }

                Section("Exercises") {
                    NavigationLink("Manage Exercises") { ManageExercisesView() }
                }

                Section("Templates") {
                    NavigationLink("Manage Templates") { ManageTemplatesView() }
                }
            }
            .navigationTitle("Settings")
        }
    }
}

struct ManageExercisesView: View {
    @Environment(\.modelContext) private var context
    @Query(sort: [\.name]) private var exercises: [Exercise]
    @State private var name: String = ""

    var body: some View {
        Form {
            Section("Add") {
                HStack { TextField("Name", text: $name); Button("Add") { add() } }
            }
            Section("All") {
                ForEach(exercises) { ex in
                    NavigationLink(ex.name) { ExerciseDetailView(exercise: ex) }
                }
            }
        }.navigationTitle("Exercises")
    }
    func add() {
        guard !name.isEmpty else { return }
        context.insert(Exercise(name: name))
        name = ""
    }
}

struct ManageTemplatesView: View {
    @Environment(\.modelContext) private var context
    @Query(sort: [\.name]) private var templates: [Template]
    @State private var name: String = ""

    var body: some View {
        Form {
            Section("Add Template") {
                HStack { TextField("Name", text: $name); Button("Add") { add() } }
            }
            Section("All Templates") {
                ForEach(templates) { t in
                    NavigationLink(t.name) { TemplateDetailView(template: t) }
                }
            }
        }
        .navigationTitle("Templates")
    }
    func add() {
        guard !name.isEmpty else { return }
        let t = Template(name: name)
        context.insert(t)
        name = ""
    }
}

struct TemplateDetailView: View {
    @Environment(\.modelContext) private var context
    @Query(sort: [\.name]) private var exercises: [Exercise]
    @State var template: Template

    @State private var selectedExercise: Exercise?
    @State private var targetSets: Int = 3
    @State private var targetReps: Int = 5
    @State private var defaultRPE: Double = 8.0
    @State private var defaultRest: Int = 120

    var body: some View {
        Form {
            Section("Add Item") {
                Picker("Exercise", selection: $selectedExercise) {
                    ForEach(exercises) { ex in Text(ex.name).tag(Optional(ex)) }
                }
                Stepper(value: $targetSets, in: 1...10) { Text("Sets: \(targetSets)") }
                Stepper(value: $targetReps, in: 1...30) { Text("Reps: \(targetReps)") }
                HStack { Slider(value: $defaultRPE, in: 6...10, step: 0.5) { Text("RPE") }; Text(defaultRPE, format: .number.precision(.fractionLength(1))) }
                Stepper(value: $defaultRest, in: 30...600, step: 15) { Text("Rest: \(defaultRest)s") }
                Button("Add Item") { addItem() }
            }
            Section("Items") {
                ForEach(template.items) { item in
                    HStack {
                        Text(item.exercise.name)
                        Spacer()
                        Text("\(item.targetSets)x\(item.targetReps)")
                    }
                }
            }
        }
        .navigationTitle(template.name)
    }
    func addItem() {
        guard let ex = selectedExercise else { return }
        let item = TemplateItem(exercise: ex, targetSets: targetSets, targetReps: targetReps, defaultRPE: defaultRPE, defaultRestSec: defaultRest)
        template.items.append(item)
    }
}

struct ExerciseDetailView: View {
    @Environment(\.modelContext) private var context
    @Query(sort: [\.name]) private var tags: [Tag]
    @State var exercise: Exercise
    @State private var newTagName: String = ""

    var body: some View {
        Form {
            Section("Tags") {
                ForEach(tags) { tag in
                    HStack {
                        Text(tag.name)
                        Spacer()
                        let hasTag = exercise.tags.contains(where: { $0.name == tag.name })
                        Image(systemName: hasTag ? "checkmark.circle.fill" : "circle")
                            .onTapGesture { toggle(tag) }
                    }
                }
                HStack { TextField("New tag", text: $newTagName); Button("Add") { addTag() } }
            }
        }
        .navigationTitle(exercise.name)
    }
    func toggle(_ tag: Tag) {
        if let idx = exercise.tags.firstIndex(where: { $0.name == tag.name }) {
            exercise.tags.remove(at: idx)
        } else {
            exercise.tags.append(tag)
        }
    }
    func addTag() {
        guard !newTagName.isEmpty else { return }
        let tag = Tag(name: newTagName)
        context.insert(tag)
        exercise.tags.append(tag)
        newTagName = ""
    }
}

#Preview { SettingsView() }
