import Foundation
import UserNotifications

enum NotificationService {
    static func requestAuthorization() async {
        do {
            _ = try await UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge])
        } catch {
            // Ignore for MVP
        }
    }

    @discardableResult
    static func scheduleRestTimer(seconds: Int) async -> String? {
        let content = UNMutableNotificationContent()
        content.title = NSLocalizedString("Rest Complete", comment: "")
        content.body = NSLocalizedString("Time to start your next set.", comment: "")
        content.sound = .default

        let trigger = UNTimeIntervalNotificationTrigger(timeInterval: TimeInterval(seconds), repeats: false)
        let id = UUID().uuidString
        let request = UNNotificationRequest(identifier: id, content: content, trigger: trigger)
        do {
            try await UNUserNotificationCenter.current().add(request)
            return id
        } catch {
            return nil
        }
    }

    static func cancelNotification(id: String) {
        UNUserNotificationCenter.current().removePendingNotificationRequests(withIdentifiers: [id])
    }
}

