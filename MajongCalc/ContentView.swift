import SwiftUI

struct ContentView: View {
    @ObservedObject var viewModel: ScoringViewModel

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    contextSection
                    doraSection
                    fuSection
                    yakuSection
                    voiceSection
                    resultSection
                }
                .padding()
            }
            .navigationTitle("麻雀点数計算")
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("クリア", action: viewModel.clearAll)
                }
            }
        }
    }

    private var contextSection: some View {
        GroupBox(label: Text("基本情報")) {
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Picker("和了", selection: $viewModel.hand.winType) {
                        Text("ロン").tag(WinType.ron)
                        Text("ツモ").tag(WinType.tsumo)
                    }.pickerStyle(.segmented)
                }
                Toggle("親（親ならON）", isOn: $viewModel.hand.isDealer)
                Toggle("門前（副露があればOFF）", isOn: $viewModel.hand.menzen)
                HStack {
                    Stepper("本場: \(viewModel.hand.honba)", value: $viewModel.hand.honba, in: 0...20)
                    Stepper("供託: \(viewModel.hand.riichiSticks)", value: $viewModel.hand.riichiSticks, in: 0...10)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private var doraSection: some View {
        GroupBox(label: Text("ドラ")) {
            HStack {
                Stepper("ドラ: \(viewModel.hand.dora)", value: $viewModel.hand.dora, in: 0...20)
                Stepper("赤: \(viewModel.hand.akaDora)", value: $viewModel.hand.akaDora, in: 0...10)
                Stepper("裏: \(viewModel.hand.uraDora)", value: $viewModel.hand.uraDora, in: 0...20)
            }
        }
    }

    private var fuSection: some View {
        GroupBox(label: Text("符")) {
            HStack {
                Stepper("符: \(viewModel.hand.manualFu ?? ScoreCalculator.autoFu(for: viewModel.hand))", onIncrement: {
                    var fu = viewModel.hand.manualFu ?? ScoreCalculator.autoFu(for: viewModel.hand)
                    fu = fu == 25 ? 30 : fu // 25→30 以降10刻み
                    fu += 10
                    viewModel.hand.manualFu = fu
                }, onDecrement: {
                    var fu = viewModel.hand.manualFu ?? ScoreCalculator.autoFu(for: viewModel.hand)
                    fu = max(20, fu == 30 ? 25 : fu - 10)
                    viewModel.hand.manualFu = fu
                })
                Button("自動") { viewModel.autoSetFu() }
                Button("手動解除") { viewModel.hand.manualFu = nil }
            }
        }
    }

    private var yakuSection: some View {
        GroupBox(label: Text("役を選択（音声でも可）")) {
            VStack(alignment: .leading, spacing: 8) {
                yakuGrid(
                    [
                        (.riichi, "リーチ"), (.doubleRiichi, "ダブルリーチ"), (.ippatsu, "一発"), (.menzenTsumo, "門前ツモ"), (.pinfu, "平和"), (.tanyao, "タンヤオ"), (.iipeikou, "一盃口")
                    ]
                )
                Divider()
                yakuGrid(
                    [
                        (.haku, "白"), (.hatsu, "發"), (.chun, "中"), (.seatWind, "自風"), (.roundWind, "場風"), (.haitei, "海底"), (.houtei, "河底"), (.rinshan, "嶺上"), (.chankan, "槍槓"), (.nagashiMangan, "流し満貫")
                    ]
                )
                Divider()
                yakuGrid(
                    [
                        (.sanshokuDojun, "三色同順"), (.ittsu, "一気通貫"), (.toitoi, "対々和"), (.sanankou, "三暗刻"), (.sanshokuDokou, "三色同刻"), (.sankantsu, "三槓子"), (.chanta, "混全帯么九"), (.chiitoitsu, "七対子"), (.shousangen, "小三元"), (.honroutou, "混老頭"), (.junchan, "純全帯么九"), (.honitsu, "混一色"), (.chinitsu, "清一色")
                    ]
                )
                Divider()
                Text("役満")
                yakuGrid(
                    [
                        (.renhou, "人和"), (.kokushi, "国士無双"), (.daisangen, "大三元"), (.suuAnkou, "四暗刻"), (.suuAnkouTanki, "四暗刻単騎"), (.tsuuiisou, "字一色"), (.ryuuiisou, "緑一色"), (.chinroutou, "清老頭"), (.shousuushi, "小四喜"), (.daisushi, "大四喜"), (.suuKantsu, "四槓子"), (.chuuren, "九蓮宝燈"), (.junseiChuuren, "純正九蓮"), (.tenhou, "天和"), (.chihou, "地和"), (.jusanFuta, "十三不塔")
                    ]
                )
            }
        }
    }

    private func yakuGrid(_ items: [(Yaku, String)]) -> some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 110), spacing: 8, alignment: .leading)], alignment: .leading, spacing: 8) {
            ForEach(items, id: \.(0)) { item in
                let (y, label) = item
                Toggle(label, isOn: Binding(
                    get: { viewModel.hand.selectedYaku.contains(y) },
                    set: { newVal in
                        if newVal { viewModel.hand.selectedYaku.insert(y) } else { viewModel.hand.selectedYaku.remove(y) }
                    }
                ))
                .toggleStyle(.button)
                .buttonStyle(.bordered)
            }
        }
    }

    private var voiceSection: some View {
        GroupBox(label: Text("音声入力")) {
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Button(action: viewModel.toggleListening) {
                        Label(viewModel.speech.isListening ? "停止" : "開始", systemImage: viewModel.speech.isListening ? "stop.circle" : "mic.circle")
                    }
                    .buttonStyle(.borderedProminent)
                    if !viewModel.speech.isAuthorized {
                        Text("設定で音声認識の許可が必要です").foregroundColor(.orange)
                    }
                }
                Text("認識中: \(viewModel.lastVoiceText)").font(.footnote).foregroundColor(.secondary)
            }
        }
    }

    private var resultSection: some View {
        GroupBox(label: Text("結果")) {
            let r = viewModel.result
            let adj = ScoreCalculator.applyHonbaAndRiichi(to: r, hand: viewModel.hand)
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Text("翻: \(r.han)")
                    Text("符: \(r.fu)")
                    if let l = r.limitLabel { Text(l).bold() }
                }
                switch viewModel.hand.winType {
                case .ron:
                    if let ron = adj.ron { Text("ロン: \(ron) 点（供託含む）") }
                case .tsumo:
                    if viewModel.hand.isDealer, let each = adj.tsumo.dealerEach {
                        let total = each * 3 + viewModel.hand.riichiSticks * 1000
                        Text("ツモ: \(each) オール（合計 \(total)）")
                    } else if let fd = adj.tsumo.fromDealer, let fo = adj.tsumo.fromOthers {
                        let total = fd + fo * 2 + viewModel.hand.riichiSticks * 1000
                        Text("ツモ: 親 \(fd) / 子 \(fo)（合計 \(total)）")
                    }
                }
                if r.basePoints > 0 {
                    Text("基本点: \(r.basePoints)").font(.footnote).foregroundColor(.secondary)
                }
            }
        }
    }
}

struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView(viewModel: ScoringViewModel())
    }
}
