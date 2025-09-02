import Foundation

enum LimitTier: Equatable {
    case none
    case mangan
    case haneman
    case baiman
    case sanbaiman
    case yakuman(multiplier: Int)

    var label: String? {
        switch self {
        case .none: return nil
        case .mangan: return "満貫"
        case .haneman: return "跳満"
        case .baiman: return "倍満"
        case .sanbaiman: return "三倍満"
        case .yakuman(let m): return m > 1 ? "役満x\(m)" : "役満"
        }
    }
}

struct ScoreCalculator {
    static func roundUpTo100(_ x: Int) -> Int { ((x + 99) / 100) * 100 }

    static func autoFu(for hand: HandState) -> Int {
        if hand.selectedYaku.contains(.chiitoitsu) { return 25 }
        if hand.selectedYaku.contains(.pinfu) {
            return hand.winType == .tsumo ? 20 : 30
        }
        return 30
    }

    static func totalHan(for hand: HandState, rules: Rules) -> (han: Int, yakumanMul: Int) {
        let isOpen = !hand.menzen
        var han = 0
        var yakumanMul = 0

        for y in hand.selectedYaku {
            if y.isYakuman {
                yakumanMul += y.yakumanMultiplier(rules: rules)
            } else {
                han += y.han(menzen: hand.menzen, open: isOpen, rules: rules)
            }
        }

        // Dora do not count as yaku but as han
        han += hand.dora + (rules.allowAkaDora > 0 ? hand.akaDora : 0) + (rules.allowUraDora ? hand.uraDora : 0)
        return (han, yakumanMul)
    }

    static func limitTier(han: Int, fu: Int, yakumanMul: Int, rules: Rules) -> LimitTier {
        // Special-case limit yaku like 流し満貫
        // Note: callers ensure hand context is available
        if yakumanMul > 0 { return .yakuman(multiplier: yakumanMul) }
        if rules.allowKazoeYakuman && han >= 13 { return .yakuman(multiplier: 1) }
        if han >= 11 { return .sanbaiman }
        if han >= 8 { return .baiman }
        if han >= 6 { return .haneman }
        if han >= 5 { return .mangan }
        if han == 4 && fu >= 40 { return .mangan }
        if han == 3 && fu >= 70 { return .mangan }
        if rules.allowKiriageMangan && ((han == 4 && fu == 30) || (han == 3 && fu == 60)) { return .mangan }
        return .none
    }

    static func calculate(hand: HandState, rules: Rules) -> ScoreResult {
        let (totalHan, yakumanMul) = totalHan(for: hand, rules: rules)
        let fu = hand.manualFu ?? autoFu(for: hand)
        // Special: 流し満貫
        let hasNagashi = hand.selectedYaku.contains { y in if case .nagashiMangan = y { return true } else { return false } }
        let tier: LimitTier = hasNagashi ? .mangan : limitTier(han: totalHan, fu: fu, yakumanMul: yakumanMul, rules: rules)

        let basePoints: Int
        let payments: ScoreResult.Payments

        func paymentsForBase(_ base: Int, isDealer: Bool, winType: WinType) -> ScoreResult.Payments {
            switch winType {
            case .ron:
                if isDealer {
                    return .init(ronPoints: roundUpTo100(base * 6), tsumoNonDealerFromDealer: nil, tsumoNonDealerFromNonDealer: nil, tsumoDealerEach: nil)
                } else {
                    return .init(ronPoints: roundUpTo100(base * 4), tsumoNonDealerFromDealer: nil, tsumoNonDealerFromNonDealer: nil, tsumoDealerEach: nil)
                }
            case .tsumo:
                if isDealer {
                    return .init(ronPoints: nil, tsumoNonDealerFromDealer: nil, tsumoNonDealerFromNonDealer: nil, tsumoDealerEach: roundUpTo100(base * 2))
                } else {
                    return .init(ronPoints: nil, tsumoNonDealerFromDealer: roundUpTo100(base * 2), tsumoNonDealerFromNonDealer: roundUpTo100(base * 1), tsumoDealerEach: nil)
                }
            }
        }

        switch tier {
        case .none:
            // below mangan: base = fu * 2^(han+2)
            let base = Int(Double(fu) * pow(2.0, Double(totalHan + 2)))
            basePoints = base
            payments = paymentsForBase(base, isDealer: hand.isDealer, winType: hand.winType)
        case .mangan, .haneman, .baiman, .sanbaiman, .yakuman:
            // Use fixed payments (total points). We'll map to per-share for tsumo.
            let totalRonNonDealer: Int
            switch tier {
            case .mangan: totalRonNonDealer = 8000
            case .haneman: totalRonNonDealer = 12000
            case .baiman: totalRonNonDealer = 16000
            case .sanbaiman: totalRonNonDealer = 24000
            case .yakuman(let m): totalRonNonDealer = 32000 * max(1, m)
            default: totalRonNonDealer = 0
            }
            basePoints = 0 // not meaningful at limit tiers
            switch hand.winType {
            case .ron:
                let ron = hand.isDealer ? totalRonNonDealer * 3 / 2 : totalRonNonDealer
                payments = .init(ronPoints: ron, tsumoNonDealerFromDealer: nil, tsumoNonDealerFromNonDealer: nil, tsumoDealerEach: nil)
            case .tsumo:
                if hand.isDealer {
                    // dealer tsumo each pays: mangan 4000, haneman 6000, baiman 8000, sanbaiman 12000, yakuman 16000×m
                    let each: Int
                    switch tier {
                    case .mangan: each = 4000
                    case .haneman: each = 6000
                    case .baiman: each = 8000
                    case .sanbaiman: each = 12000
                    case .yakuman(let m): each = 16000 * max(1, m)
                    default: each = 0
                    }
                    payments = .init(ronPoints: nil, tsumoNonDealerFromDealer: nil, tsumoNonDealerFromNonDealer: nil, tsumoDealerEach: each)
                } else {
                    // child tsumo: dealer pays high, others low
                    let fromDealer: Int
                    let fromOthers: Int
                    switch tier {
                    case .mangan: fromDealer = 4000; fromOthers = 2000
                    case .haneman: fromDealer = 6000; fromOthers = 3000
                    case .baiman: fromDealer = 8000; fromOthers = 4000
                    case .sanbaiman: fromDealer = 12000; fromOthers = 6000
                    case .yakuman(let m): fromDealer = 16000 * max(1, m); fromOthers = 8000 * max(1, m)
                    default: fromDealer = 0; fromOthers = 0
                    }
                    payments = .init(ronPoints: nil, tsumoNonDealerFromDealer: fromDealer, tsumoNonDealerFromNonDealer: fromOthers, tsumoDealerEach: nil)
                }
            }
        }

        var result = ScoreResult(
            han: totalHan,
            fu: fu,
            limitLabel: tier.label,
            payments: payments,
            basePoints: basePoints,
            totalYakumanMultiplier: (tier == .none) ? 0 : (tier == .yakuman(multiplier: 1) ? 1 : {
                if case .yakuman(let m) = tier { return m } else { return 0 }
            }())
        )
        return result
    }

    static func applyHonbaAndRiichi(to result: ScoreResult, hand: HandState) -> (ron: Int?, tsumo: (fromDealer: Int?, fromOthers: Int?, dealerEach: Int?)) {
        // Honba: +300 to ron (winner gains), +100 to each tsumo payer
        // Riichi sticks: winner gets +1000 * riichiSticks
        let riichiBonus = hand.riichiSticks * 1000
        let honba = hand.honba

        var ron = result.payments.ronPoints
        var fromDealer = result.payments.tsumoNonDealerFromDealer
        var fromOthers = result.payments.tsumoNonDealerFromNonDealer
        var dealerEach = result.payments.tsumoDealerEach

        if let r = ron {
            ron = r + honba * 300 + riichiBonus
        }
        if let fd = fromDealer {
            fromDealer = fd + honba * 100
            fromOthers = (fromOthers ?? 0) + honba * 100
        }
        if let de = dealerEach {
            dealerEach = de + honba * 100
        }
        // For tsumo, add riichi bonus to winner separately (not to individual payments). Report by caller.
        return (ron, (fromDealer, fromOthers, dealerEach))
    }
}
