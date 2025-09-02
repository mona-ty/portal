import Foundation

enum UnitService {
    static func toDisplayWeight(_ kg: Double, unit: UnitSystem) -> Double {
        switch unit {
        case .kg: return round(kg * 10) / 10
        case .lb: return round(kgToLb(kg) * 10) / 10
        }
    }
    static func fromDisplayWeight(_ value: Double, unit: UnitSystem) -> Double {
        switch unit {
        case .kg: return value
        case .lb: return lbToKg(value)
        }
    }
    static func kgToLb(_ kg: Double) -> Double { kg * 2.2046226218 }
    static func lbToKg(_ lb: Double) -> Double { lb / 2.2046226218 }
}

