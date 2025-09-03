import Foundation
import SwiftData

@MainActor
final class HistoryViewModel: ObservableObject {
    func workouts(on day: Date, in context: ModelContext) throws -> [Workout] {
        let start = Calendar.current.startOfDay(for: day)
        let end = Calendar.current.date(byAdding: .day, value: 1, to: start)!
        let predicate = #Predicate<Workout> { $0.date >= start && $0.date < end }
        let descriptor = FetchDescriptor<Workout>(predicate: predicate, sortBy: [.init(\.date, order: .reverse)])
        return try context.fetch(descriptor)
    }

    func workouts(matching text: String, in context: ModelContext) throws -> [Workout] {
        guard !text.isEmpty else {
            let descriptor = FetchDescriptor<Workout>(sortBy: [.init(\.date, order: .reverse)])
            return try context.fetch(descriptor)
        }
        let predicate = #Predicate<Workout> { workout in
            workout.note?.localizedStandardContains(text) == true ||
            workout.sets.contains { set in
                set.exercise.name.localizedStandardContains(text) ||
                set.exercise.tags.contains(where: { $0.name.localizedStandardContains(text) })
            }
        }
        let descriptor = FetchDescriptor<Workout>(predicate: predicate, sortBy: [.init(\.date, order: .reverse)])
        return try context.fetch(descriptor)
    }
}
