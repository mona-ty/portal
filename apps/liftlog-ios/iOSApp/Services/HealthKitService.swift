import Foundation
import HealthKit

final class HealthKitService {
    static let shared = HealthKitService()
    private let healthStore = HKHealthStore()

    private init() {}

    func isAvailable() -> Bool { HKHealthStore.isHealthDataAvailable() }

    func requestAuthorization() async throws {
        let readTypes: Set = [
            HKObjectType.quantityType(forIdentifier: .bodyMass)!,
            HKObjectType.workoutType()
        ]
        let writeTypes: Set = [
            HKObjectType.workoutType()
        ]
        try await healthStore.requestAuthorization(toShare: writeTypes, read: readTypes)
    }

    func latestBodyMassKg() async throws -> Double? {
        let type = HKQuantityType.quantityType(forIdentifier: .bodyMass)!
        let sort = NSSortDescriptor(key: HKSampleSortIdentifierEndDate, ascending: false)
        let query = HKSampleQueryDescriptor(predicates: [.sample(type: type)], sortDescriptors: [sort], limit: 1)
        let results = try await query.result(for: healthStore)
        guard let quantitySample = results.first as? HKQuantitySample else { return nil }
        let kg = quantitySample.quantity.doubleValue(for: .gramUnit(with: .kilo))
        return kg
    }

    func saveWorkout(start: Date, end: Date, totalEnergy: Double? = nil, metadata: [String: Any]? = nil) async throws {
        let workout = HKWorkout(activityType: .traditionalStrengthTraining, start: start, end: end, workoutEvents: nil, totalEnergyBurned: totalEnergy != nil ? HKQuantity(unit: .kilocalorie(), doubleValue: totalEnergy!) : nil, totalDistance: nil, device: .local(), metadata: metadata)
        try await healthStore.save(workout)
    }
}

