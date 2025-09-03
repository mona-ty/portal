import SwiftUI
import SwiftData

@main
struct WorkoutLogApp: App {
    var body: some Scene {
        WindowGroup {
            MainTabView()
        }
        .modelContainer(for: [Exercise.self, Workout.self, SetRecord.self, Tag.self, Template.self])
    }
}

