# LiftLog (MVP Scaffold)

An iPhone-only strength training log app scaffold. Built for iOS 17+ using SwiftUI and SwiftData. Includes logging exercises/sets (weight, reps, RPE), rest timer with notifications, history/search (by day, exercise, tag), analytics (1RM estimate, volume, PRs), templates/routines, HealthKit integration, widgets, Siri Shortcuts, dark mode, and English/Japanese UI.

Note: This repo contains a code skeleton without an Xcode project. Follow the setup steps below to create an Xcode project and enable capabilities.

## Tech Stack
- iOS 17+
- SwiftUI + SwiftData (local-only persistence)
- HealthKit (read body mass, write workouts)
- UserNotifications (rest timer alerts)
- WidgetKit (home screen widget — placeholder code included)
- AppIntents (Siri Shortcuts)
- Localization (en, ja)

## Features in MVP
- Log: Exercise/Set (weight, reps, RPE), rest timer, notes
- History & Search: by day, exercise, tag
- Analytics: 1RM estimate (Epley/Brzycki), total volume, PR tracking
- Templates: create/apply routines
- Units: kg/lb toggle
- Health: read body weight; write workouts sessions
- Notifications: rest timer local notifications
- Widgets: basic widget placeholder
- Siri Shortcuts: start workout, log a set, start rest

## Folder Structure
- `iOSApp/` — App code (SwiftUI app entry, views, models, services)
- `iOSApp/Models/` — SwiftData models (@Model)
- `iOSApp/Services/` — HealthKit/Notifications/Analytics/PR/Units/Settings
- `iOSApp/ViewModels/` — Observable view models
- `iOSApp/Views/` — SwiftUI screens (Today, Record, History, Analysis, Settings)
- `iOSApp/AppIntents/` — Siri Shortcuts definitions
- `Widgets/` — WidgetKit placeholder
- `Localization/` — Localizable.strings (en, ja)

## Create Xcode Project (Xcode 15+)
1) In Xcode: File > New > Project… > iOS > App
   - Product Name: `LiftLog`
   - Interface: SwiftUI
   - Language: Swift
   - Include Tests: optional
   - Minimum iOS: 17.0

2) Add the source files:
   - Drag the `iOSApp`, `Localization`, `Widgets` folders into the project (Create folder references or groups as you prefer).

3) App Capabilities:
   - Signing & Capabilities > + Capability > add:
     - HealthKit (check Workout Types; read Body Mass; write Workouts)
     - Background Modes (for widgets if needed; not required for timer)
     - Push Notifications is NOT needed; only Local Notifications (no capability needed).
     - App Groups not required for MVP.

4) Info.plist additions (if not auto-managed):
   - `NSHealthShareUsageDescription` = "Allow reading body weight to personalize training."
   - `NSHealthUpdateUsageDescription` = "Allow saving workouts to Health."

5) Local Notifications:
   - First launch: app requests authorization in `NotificationService`.

6) Widgets target:
   - File > New > Target… > Widget Extension.
   - Name: `LiftLogWidgets`.
   - Add `Widgets/*.swift` files to that target.

7) App Intents (Siri Shortcuts):
   - No separate extension is required for App Shortcuts (iOS 16+). Ensure the app target includes the `iOSApp/AppIntents/*` files.

8) SwiftData setup:
   - iOS 17 app template includes `@main` App. The included code already wraps the model container.

9) Build and run on device/simulator (iOS 17).

## Key Assumptions (please confirm)
- Minimum iOS: 17.0 (to use SwiftData + AppIntents cleanly). If you require iOS 16, we can switch to Core Data and maintain Shortcuts with some adjustments.
- RPE scale: 6.0–10.0 step 0.5 (configurable). Let me know your preferred scale.
- PR definition: best single-set weight for given reps OR best estimated 1RM per exercise. Both are included; you can choose which to display by default.
- Tags: free-text, many-to-many with exercises. If you prefer a controlled vocabulary, we can add a Tag library.

## Next Steps
- Confirm assumptions above.
- I can wire up an actual Xcode project in-repo if you prefer (more files, but ready-to-open). Or keep the code-only scaffold if you prefer to create the project locally.
- After your confirmation, I will expand the widget to display today’s volume/next set and finalize the App Shortcuts phrases (en/ja).

