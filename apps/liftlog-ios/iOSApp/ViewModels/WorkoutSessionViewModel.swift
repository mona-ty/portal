import Foundation
import Combine
import SwiftData

@MainActor
final class WorkoutSessionViewModel: ObservableObject {
    @Published var activeWorkout: Workout?
    @Published var activeTemplate: Template?
    @Published var secondsRemaining: Int = 0
    @Published var isResting: Bool = false
    @Published var scheduledNotificationId: String?

    private var timerCancellable: AnyCancellable?
    private var startDate: Date?

    func startNewWorkout(context: ModelContext, unit: UnitSystem) {
        let workout = Workout(date: Date(), unit: unit, note: nil, durationSec: 0, sets: [])
        context.insert(workout)
        activeWorkout = workout
        startDate = Date()
        activeTemplate = nil
    }

    func startWithTemplate(context: ModelContext, unit: UnitSystem, template: Template) {
        startNewWorkout(context: context, unit: unit)
        activeTemplate = template
    }

    func addSet(context: ModelContext, exercise: Exercise, displayWeight: Double, unit: UnitSystem, reps: Int, rpe: Double?, isWarmup: Bool, restSec: Int?) {
        guard let workout = activeWorkout else { return }
        let kg = UnitService.fromDisplayWeight(displayWeight, unit: unit)
        let set = SetRecord(exercise: exercise, weight: kg, reps: reps, rpe: rpe, isWarmup: isWarmup, restSecPlanned: restSec, timestamp: Date())
        workout.sets.append(set)
        if let rest = restSec { startRest(seconds: rest) }
    }

    func startRest(seconds: Int) {
        secondsRemaining = seconds
        isResting = true
        timerCancellable?.cancel()
        timerCancellable = Timer.publish(every: 1, on: .main, in: .common).autoconnect().sink { [weak self] _ in
            guard let self else { return }
            self.secondsRemaining -= 1
            if self.secondsRemaining <= 0 {
                self.stopRest()
            }
        }
        Task { [seconds] in
            self.scheduledNotificationId = await NotificationService.scheduleRestTimer(seconds: seconds)
        }
    }

    func stopRest() {
        isResting = false
        timerCancellable?.cancel()
        if let id = scheduledNotificationId { NotificationService.cancelNotification(id: id) }
        scheduledNotificationId = nil
    }

    func endWorkout() {
        timerCancellable?.cancel()
        if let start = startDate, let workout = activeWorkout {
            workout.durationSec = Int(Date().timeIntervalSince(start))
        }
        activeWorkout = nil
    }
}
