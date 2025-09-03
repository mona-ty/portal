import AppIntents
import SwiftUI

struct LiftLogShortcuts: AppShortcutsProvider {
    static var shortcutTileColor: ShortcutTileColor = .green

    static var appShortcuts: [AppShortcut] {
        [
            AppShortcut(intent: StartWorkoutIntent(), phrases: [
                "Start a workout in \(.applicationName)",
                "\(.applicationName) start workout"
            ], shortTitle: "Start Workout", systemImageName: "play.fill"),
            AppShortcut(intent: StartRestIntent(), phrases: [
                "Start rest in \(.applicationName)",
                "\(.applicationName) start rest"
            ], shortTitle: "Start Rest", systemImageName: "timer")
        ]
    }
}

struct StartWorkoutIntent: AppIntent {
    static var title: LocalizedStringResource = "Start Workout"
    static var description = IntentDescription("Start a new workout with default unit.")

    func perform() async throws -> some IntentResult {
        // The app can react via onOpenURL or scene phase; for MVP we just return success
        return .result()
    }
}

struct StartRestIntent: AppIntent {
    static var title: LocalizedStringResource = "Start Rest"
    static var description = IntentDescription("Start a rest timer.")

    @Parameter(title: "Seconds") var seconds: Int

    static var parameterSummary: some ParameterSummary {
        Summary("Start rest for \(\.$seconds) seconds")
    }

    func perform() async throws -> some IntentResult {
        _ = await NotificationService.scheduleRestTimer(seconds: seconds)
        return .result()
    }
}

