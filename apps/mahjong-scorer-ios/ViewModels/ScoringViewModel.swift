import Foundation
import Combine

final class ScoringViewModel: ObservableObject {
    @Published var rules = Rules()
    @Published var hand = HandState()
    @Published var result: ScoreResult = ScoreCalculator.calculate(hand: HandState(), rules: Rules())
    @Published var lastVoiceText: String = ""

    let speech = SpeechRecognizer()
    private var cancellables = Set<AnyCancellable>()

    init() {
        speech.$transcript
            .receive(on: DispatchQueue.main)
            .sink { [weak self] t in
                guard let self else { return }
                self.lastVoiceText = t
                self.applyVoice(text: t)
            }.store(in: &cancellables)

        Publishers.CombineLatest($hand, $rules)
            .debounce(for: .milliseconds(50), scheduler: DispatchQueue.main)
            .sink { [weak self] hand, rules in
                guard let self else { return }
                self.result = ScoreCalculator.calculate(hand: hand, rules: rules)
            }.store(in: &cancellables)
    }

    func toggleListening() {
        if speech.isListening {
            speech.stop()
        } else {
            try? speech.start()
        }
    }

    func clearAll() {
        hand = HandState()
        lastVoiceText = ""
    }

    func autoSetFu() { hand.manualFu = ScoreCalculator.autoFu(for: hand) }

    // MARK: - Voice mapping
    func applyVoice(text: String) {
        let s = text.replacingOccurrences(of: " ", with: "")

        func hit(_ keys: [String]) -> Bool { keys.contains { s.contains($0) } }

        // Win type / basic context
        if hit(["ツモ", "自摸", "門前清自摸和"]) { hand.winType = .tsumo; hand.selectedYaku.insert(.menzenTsumo) }
        if hit(["ロン"]) { hand.winType = .ron }
        if hit(["親"]) { hand.isDealer = true }
        if hit(["子"]) { hand.isDealer = false }
        if hit(["門前", "メンゼン"]) { hand.menzen = true }
        if hit(["鳴き", "副露", "フーロ"]) { hand.menzen = false }

        // Core yaku
        if hit(["リーチ"]) { hand.selectedYaku.insert(.riichi) }
        if hit(["ダブルリーチ", "ダブリー"]) { hand.selectedYaku.insert(.doubleRiichi) }
        if hit(["一発"]) { hand.selectedYaku.insert(.ippatsu) }
        if hit(["平和"]) { hand.selectedYaku.insert(.pinfu) }
        if hit(["タンヤオ", "断幺九"]) { hand.selectedYaku.insert(.tanyao) }
        if hit(["一盃口"]) { hand.selectedYaku.insert(.iipeikou) }
        if hit(["三色同順", "三色"]) { hand.selectedYaku.insert(.sanshokuDojun) }
        if hit(["一気通貫", "イッツー"]) { hand.selectedYaku.insert(.ittsu) }
        if hit(["対々和", "トイトイ"]) { hand.selectedYaku.insert(.toitoi) }
        if hit(["三暗刻"]) { hand.selectedYaku.insert(.sanankou) }
        if hit(["三色同刻"]) { hand.selectedYaku.insert(.sanshokuDokou) }
        if hit(["三槓子"]) { hand.selectedYaku.insert(.sankantsu) }
        if hit(["混全帯么九", "チャンタ"]) { hand.selectedYaku.insert(.chanta) }
        if hit(["純全帯么九", "純チャン", "ジュンチャン"]) { hand.selectedYaku.insert(.junchan) }
        if hit(["混老頭"]) { hand.selectedYaku.insert(.honroutou) }
        if hit(["小三元"]) { hand.selectedYaku.insert(.shousangen) }
        if hit(["清一色", "チンイツ"]) { hand.selectedYaku.insert(.chinitsu) }
        if hit(["混一色", "ホンイツ"]) { hand.selectedYaku.insert(.honitsu) }
        if hit(["七対子", "チートイツ"]) { hand.selectedYaku.insert(.chiitoitsu) }

        // Yakuhai
        if hit(["白"]) { hand.selectedYaku.insert(.haku) }
        if hit(["發", "発"]) { hand.selectedYaku.insert(.hatsu) }
        if hit(["中"]) { hand.selectedYaku.insert(.chun) }
        if hit(["場風"]) { hand.selectedYaku.insert(.roundWind) }
        if hit(["自風"]) { hand.selectedYaku.insert(.seatWind) }

        // Situational
        if hit(["海底", "海底摸月"]) { hand.selectedYaku.insert(.haitei) }
        if hit(["河底", "河底撈魚"]) { hand.selectedYaku.insert(.houtei) }
        if hit(["嶺上", "嶺上開花"]) { hand.selectedYaku.insert(.rinshan) }
        if hit(["槍槓"]) { hand.selectedYaku.insert(.chankan) }
        if hit(["流し満貫", "流し"]) { hand.selectedYaku.insert(.nagashiMangan) }

        // Yakuman
        if hit(["人和"]) { hand.selectedYaku.insert(.renhou) }
        if hit(["国士無双", "国士"]) { hand.selectedYaku.insert(.kokushi) }
        if hit(["大三元"]) { hand.selectedYaku.insert(.daisangen) }
        if hit(["四暗刻単騎"]) { hand.selectedYaku.insert(.suuAnkouTanki) }
        else if hit(["四暗刻"]) { hand.selectedYaku.insert(.suuAnkou) }
        if hit(["字一色"]) { hand.selectedYaku.insert(.tsuuiisou) }
        if hit(["緑一色"]) { hand.selectedYaku.insert(.ryuuiisou) }
        if hit(["清老頭"]) { hand.selectedYaku.insert(.chinroutou) }
        if hit(["小四喜"]) { hand.selectedYaku.insert(.shousuushi) }
        if hit(["大四喜"]) { hand.selectedYaku.insert(.daisushi) }
        if hit(["四槓子"]) { hand.selectedYaku.insert(.suuKantsu) }
        if hit(["九蓮宝燈"]) { hand.selectedYaku.insert(.chuuren) }
        if hit(["純正九蓮"]) { hand.selectedYaku.insert(.junseiChuuren) }
        if hit(["天和"]) { hand.selectedYaku.insert(.tenhou) }
        if hit(["地和"]) { hand.selectedYaku.insert(.chihou) }
        if hit(["十三不塔"]) { hand.selectedYaku.insert(.jusanFuta) }

        // Dora counts like "ドラ3", "赤ドラ2", "裏ドラ1"
        let patterns: [(label: String, setter: (Int) -> Void)] = [
            ("ドラ", { self.hand.dora = $0 }),
            ("赤ドラ", { self.hand.akaDora = $0 }),
            ("裏ドラ", { self.hand.uraDora = $0 })
        ]
        for (label, set) in patterns {
            if let range = s.range(of: label) {
                let tail = String(s[range.upperBound...])
                if let n = extractLeadingNumber(from: tail) { set(n) }
            }
        }
    }

    private func extractLeadingNumber(from text: String) -> Int? {
        let prefix = String(text.prefix(3))
        if let d = prefix.first, d.isNumber, let n = Int(String(d)) { return n }
        for k in [2,3,1] {
            let p = String(prefix.prefix(k))
            if let n = JapaneseNumberParser.parse(p) { return n }
        }
        return nil
    }
}

