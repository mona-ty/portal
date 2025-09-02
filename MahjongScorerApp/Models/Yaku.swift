import Foundation

enum WinType: String, CaseIterable {
    case ron
    case tsumo
}

struct Rules {
    var allowOpenTanyao: Bool = true
    var allowAkaDora: Int = 3 // number of red fives in use (UI counts actual red doras in hand)
    var allowUraDora: Bool = true
    var allowKiriageMangan: Bool = true
    var allowKazoeYakuman: Bool = true
    var allowDoubleYakuman: Bool = true
}

enum Yaku: Hashable, CaseIterable {
    // 1 han typically
    case riichi
    case doubleRiichi
    case ippatsu
    case menzenTsumo
    case pinfu
    case tanyao
    case iipeikou
    case haku
    case hatsu
    case chun
    case seatWind
    case roundWind
    case haitei
    case houtei
    case rinshan
    case chankan

    // 2 han tier
    case sanshokuDojun
    case ittsu
    case toitoi
    case sanankou
    case sanshokuDokou
    case sankantsu
    case chanta
    case chiitoitsu
    case shousangen
    case honroutou

    // 3 han tier
    case junchan
    case honitsu

    // 6 han tier
    case chinitsu

    // Yakuman
    case renhou // �l�a�i�̗p�j
    case kokushi
    case daisangen
    case suuAnkou
    case tsuuiisou
    case ryuuiisou
    case chinroutou
    case shousuushi
    case daisushi
    case suuKantsu
    case chuuren
    case tenhou
    case chihou
    case suuAnkouTanki // double yakuman option
    case junseiChuuren // double yakuman option

    // Specials
    case nagashiMangan // �������сi���сj
    case jusanFuta // �\�O�s���i�̗p: �֋X�� �𖞁j

    var isYakuman: Bool {
        switch self {
        case .kokushi, .daisangen, .suuAnkou, .tsuuiisou, .ryuuiisou, .chinroutou, .shousuushi, .daisushi, .suuKantsu, .chuuren, .tenhou, .chihou, .suuAnkouTanki, .junseiChuuren, .renhou, .jusanFuta:
            return true
        default: return false
        }
    }

    func yakumanMultiplier(rules: Rules) -> Int {
        switch self {
            case .daisushi: return rules.allowDoubleYakuman ? 2 : 1
            case .suuAnkouTanki: return rules.allowDoubleYakuman ? 2 : 1
            case .junseiChuuren: return rules.allowDoubleYakuman ? 2 : 1
            case .kokushi, .daisangen, .suuAnkou, .tsuuiisou, .ryuuiisou, .chinroutou, .shousuushi, .suuKantsu, .chuuren, .tenhou, .chihou, .renhou, .jusanFuta:
                return 1
            default:
                return 0
        }
    }

    func han(menzen: Bool, open: Bool, rules: Rules) -> Int {
        // Returns 0 if not satisfied by hand openness; otherwise han value
        switch self {
        case .riichi: return menzen ? 1 : 0
        case .doubleRiichi: return menzen ? 2 : 0
        case .ippatsu: return 1
        case .menzenTsumo: return menzen ? 1 : 0
        case .pinfu: return menzen ? 1 : 0
        case .tanyao: return (open && !rules.allowOpenTanyao) ? 0 : 1
        case .iipeikou: return menzen ? 1 : 0
        case .haku, .hatsu, .chun, .seatWind, .roundWind: return 1
        case .haitei, .houtei, .rinshan, .chankan: return 1

        case .sanshokuDojun: return open ? 1 : 2
        case .ittsu: return open ? 1 : 2
        case .toitoi: return 2
        case .sanankou: return 2
        case .sanshokuDokou: return 2
        case .sankantsu: return 2
        case .chanta: return open ? 1 : 2
        case .chiitoitsu: return menzen ? 2 : 0
        case .shousangen: return 2
        case .honroutou: return 2

        case .junchan: return open ? 2 : 3
        case .honitsu: return open ? 2 : 3
        case .chinitsu: return open ? 5 : 6

        case .nagashiMangan, .renhou, .jusanFuta:
            return 0
        default:
            return 0
        }
    }
}

struct HandState {
    // Context
    var isDealer: Bool = false
    var winType: WinType = .ron
    var menzen: Bool = true
    var honba: Int = 0
    var riichiSticks: Int = 0

    // Selections
    var selectedYaku: Set<Yaku> = []
    var dora: Int = 0
    var akaDora: Int = 0
    var uraDora: Int = 0

    // Fu handling
    var manualFu: Int? = nil // nil = auto
}

struct ScoreResult {
    struct Payments {
        // Ron results (total winner gain before honba/riichi adjustments applied at presentation)
        var ronPoints: Int? // total points from discarder
        // Tsumo results (each payment)
        var tsumoNonDealerFromDealer: Int? // child tsumo: dealer pays
        var tsumoNonDealerFromNonDealer: Int? // child tsumo: others pay
        var tsumoDealerEach: Int? // dealer tsumo: each pays
    }

    var han: Int
    var fu: Int
    var limitLabel: String? // Mangan / Haneman / Baiman / Sanbaiman / YakumanxN
    var payments: Payments
    var basePoints: Int // ��{�_�i�v�Z�m�F�p�j
    var totalYakumanMultiplier: Int // >0 if yakuman hand
}

