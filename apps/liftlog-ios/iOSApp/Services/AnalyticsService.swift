import Foundation

enum AnalyticsService {
    struct WorkoutStats {
        let totalVolumeKg: Double
        let best1RMKg: Double
    }

    static func stats(for workout: Workout, formula: OneRMFormula = .epley) -> WorkoutStats {
        let volume = PRCalculator.totalVolumeKg(for: workout.sets)
        let best1rm = workout.sets.map { PRCalculator.estimate1RM(weightKg: $0.weight, reps: $0.reps, formula: formula) }.max() ?? 0
        return .init(totalVolumeKg: volume, best1RMKg: best1rm)
    }
}

