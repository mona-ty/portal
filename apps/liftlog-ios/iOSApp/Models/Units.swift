import Foundation

enum UnitSystem: String, Codable, CaseIterable, Identifiable {
    case kg
    case lb
    var id: String { rawValue }
}

enum OneRMFormula: String, Codable, CaseIterable, Identifiable {
    case epley
    case brzycki
    var id: String { rawValue }
}

