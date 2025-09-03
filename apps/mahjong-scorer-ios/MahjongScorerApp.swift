import SwiftUI

@main
struct MahjongScorerApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView(viewModel: ScoringViewModel())
        }
    }
}
