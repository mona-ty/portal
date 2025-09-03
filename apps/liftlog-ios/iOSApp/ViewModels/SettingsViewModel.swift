import Foundation
import SwiftUI

@MainActor
final class SettingsViewModel: ObservableObject {
    @AppStorage("unitSystem") var unitRaw: String = UnitSystem.kg.rawValue
    @AppStorage("oneRMFormula") var oneRMRaw: String = OneRMFormula.epley.rawValue

    var unit: UnitSystem {
        get { UnitSystem(rawValue: unitRaw) ?? .kg }
        set { unitRaw = newValue.rawValue }
    }
    var oneRMFormula: OneRMFormula {
        get { OneRMFormula(rawValue: oneRMRaw) ?? .epley }
        set { oneRMRaw = newValue.rawValue }
    }
}

