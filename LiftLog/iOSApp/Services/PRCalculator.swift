import Foundation

enum PRCalculator {
    static func estimate1RM(weightKg: Double, reps: Int, formula: OneRMFormula) -> Double {
        guard reps > 0 else { return 0 }
        switch formula {
        case .epley:
            return weightKg * (1 + Double(reps) / 30.0)
        case .brzycki:
            let denom = 1.0278 - 0.0278 * Double(reps)
            return denom > 0 ? weightKg / denom : 0
        }
    }

    static func totalVolumeKg(for sets: [SetRecord]) -> Double {
        sets.reduce(0) { $0 + ($1.weight * Double($1.reps)) }
    }
}

