import Foundation
import SwiftData

@Model
final class Workout {
    @Attribute(.unique) var id: UUID
    var date: Date
    var unit: UnitSystem
    var note: String?
    var durationSec: Int
    @Relationship var sets: [SetRecord]

    init(id: UUID = UUID(), date: Date = .init(), unit: UnitSystem = .kg, note: String? = nil, durationSec: Int = 0, sets: [SetRecord] = []) {
        self.id = id
        self.date = date
        self.unit = unit
        self.note = note
        self.durationSec = durationSec
        self.sets = sets
    }
}

@Model
final class SetRecord {
    @Attribute(.unique) var id: UUID
    var exercise: Exercise
    var weight: Double // stored in kg
    var reps: Int
    var rpe: Double?
    var isWarmup: Bool
    var restSecPlanned: Int?
    var timestamp: Date

    init(id: UUID = UUID(), exercise: Exercise, weight: Double, reps: Int, rpe: Double? = nil, isWarmup: Bool = false, restSecPlanned: Int? = nil, timestamp: Date = .init()) {
        self.id = id
        self.exercise = exercise
        self.weight = weight
        self.reps = reps
        self.rpe = rpe
        self.isWarmup = isWarmup
        self.restSecPlanned = restSecPlanned
        self.timestamp = timestamp
    }
}

@Model
final class TemplateItem {
    var exercise: Exercise
    var targetSets: Int
    var targetReps: Int
    var defaultRPE: Double?
    var defaultRestSec: Int?
    init(exercise: Exercise, targetSets: Int, targetReps: Int, defaultRPE: Double? = nil, defaultRestSec: Int? = nil) {
        self.exercise = exercise
        self.targetSets = targetSets
        self.targetReps = targetReps
        self.defaultRPE = defaultRPE
        self.defaultRestSec = defaultRestSec
    }
}

@Model
final class Template {
    @Attribute(.unique) var id: UUID
    var name: String
    @Relationship var items: [TemplateItem]
    init(id: UUID = UUID(), name: String, items: [TemplateItem] = []) {
        self.id = id
        self.name = name
        self.items = items
    }
}

