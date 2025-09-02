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
            .navigationTitle("�����_���v�Z")
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("�N���A", action: viewModel.clearAll)
                }
            }
        }
    }

    private var contextSection: some View {
        GroupBox(label: Text("��{���")) {
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Picker("�a��", selection: $viewModel.hand.winType) {
                        Text("����").tag(WinType.ron)
                        Text("�c��").tag(WinType.tsumo)
                    }.pickerStyle(.segmented)
                }
                Toggle("�e�i�e�Ȃ�ON�j", isOn: $viewModel.hand.isDealer)
                Toggle("��O�i���I�������OFF�j", isOn: $viewModel.hand.menzen)
                HStack {
                    Stepper("�{��: \(viewModel.hand.honba)", value: $viewModel.hand.honba, in: 0...20)
                    Stepper("����: \(viewModel.hand.riichiSticks)", value: $viewModel.hand.riichiSticks, in: 0...10)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private var doraSection: some View {
        GroupBox(label: Text("�h��")) {
            HStack {
                Stepper("�h��: \(viewModel.hand.dora)", value: $viewModel.hand.dora, in: 0...20)
                Stepper("��: \(viewModel.hand.akaDora)", value: $viewModel.hand.akaDora, in: 0...10)
                Stepper("��: \(viewModel.hand.uraDora)", value: $viewModel.hand.uraDora, in: 0...20)
            }
        }
    }

    private var fuSection: some View {
        GroupBox(label: Text("��")) {
            HStack {
                Stepper("��: \(viewModel.hand.manualFu ?? ScoreCalculator.autoFu(for: viewModel.hand))", onIncrement: {
                    var fu = viewModel.hand.manualFu ?? ScoreCalculator.autoFu(for: viewModel.hand)
                    fu = fu == 25 ? 30 : fu // 25��30 �ȍ~10����
                    fu += 10
                    viewModel.hand.manualFu = fu
                }, onDecrement: {
                    var fu = viewModel.hand.manualFu ?? ScoreCalculator.autoFu(for: viewModel.hand)
                    fu = max(20, fu == 30 ? 25 : fu - 10)
                    viewModel.hand.manualFu = fu
                })
                Button("����") { viewModel.autoSetFu() }
                Button("�蓮����") { viewModel.hand.manualFu = nil }
            }
        }
    }

    private var yakuSection: some View {
        GroupBox(label: Text("����I���i�����ł��j")) {
            VStack(alignment: .leading, spacing: 8) {
                yakuGrid(
                    [
                        (.riichi, "���[�`"), (.doubleRiichi, "�_�u�����[�`"), (.ippatsu, "�ꔭ"), (.menzenTsumo, "��O�c��"), (.pinfu, "���a"), (.tanyao, "�^�����I"), (.iipeikou, "��u��")
                    ]
                )
                Divider()
                yakuGrid(
                    [
                        (.haku, "��"), (.hatsu, "�"), (.chun, "��"), (.seatWind, "����"), (.roundWind, "�ꕗ"), (.haitei, "�C��"), (.houtei, "�͒�"), (.rinshan, "���"), (.chankan, "����"), (.nagashiMangan, "��������")
                    ]
                )
                Divider()
                yakuGrid(
                    [
                        (.sanshokuDojun, "�O�F����"), (.ittsu, "��C�ʊ�"), (.toitoi, "�΁X�a"), (.sanankou, "�O�Í�"), (.sanshokuDokou, "�O�F����"), (.sankantsu, "�O�Ȏq"), (.chanta, "���S��?��"), (.chiitoitsu, "���Ύq"), (.shousangen, "���O��"), (.honroutou, "���V��"), (.junchan, "���S��?��"), (.honitsu, "����F"), (.chinitsu, "����F")
                    ]
                )
                Divider()
                Text("��")
                yakuGrid(
                    [
                        (.renhou, "�l�a"), (.kokushi, "���m���o"), (.daisangen, "��O��"), (.suuAnkou, "�l�Í�"), (.suuAnkouTanki, "�l�Í��P�R"), (.tsuuiisou, "����F"), (.ryuuiisou, "�Έ�F"), (.chinroutou, "���V��"), (.shousuushi, "���l��"), (.daisushi, "��l��"), (.suuKantsu, "�l�Ȏq"), (.chuuren, "��@��"), (.junseiChuuren, "������@"), (.tenhou, "�V�a"), (.chihou, "�n�a"), (.jusanFuta, "�\�O�s��")
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
        GroupBox(label: Text("��������")) {
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Button(action: viewModel.toggleListening) {
                        Label(viewModel.speech.isListening ? "��~" : "�J�n", systemImage: viewModel.speech.isListening ? "stop.circle" : "mic.circle")
                    }
                    .buttonStyle(.borderedProminent)
                    if !viewModel.speech.isAuthorized {
                        Text("�ݒ�ŉ����F���̋����K�v�ł�").foregroundColor(.orange)
                    }
                }
                Text("�F����: \(viewModel.lastVoiceText)").font(.footnote).foregroundColor(.secondary)
            }
        }
    }

    private var resultSection: some View {
        GroupBox(label: Text("����")) {
            let r = viewModel.result
            let adj = ScoreCalculator.applyHonbaAndRiichi(to: r, hand: viewModel.hand)
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Text("�|: \(r.han)")
                    Text("��: \(r.fu)")
                    if let l = r.limitLabel { Text(l).bold() }
                }
                switch viewModel.hand.winType {
                case .ron:
                    if let ron = adj.ron { Text("����: \(ron) �_�i�����܂ށj") }
                case .tsumo:
                    if viewModel.hand.isDealer, let each = adj.tsumo.dealerEach {
                        let total = each * 3 + viewModel.hand.riichiSticks * 1000
                        Text("�c��: \(each) �I�[���i���v \(total)�j")
                    } else if let fd = adj.tsumo.fromDealer, let fo = adj.tsumo.fromOthers {
                        let total = fd + fo * 2 + viewModel.hand.riichiSticks * 1000
                        Text("�c��: �e \(fd) / �q \(fo)�i���v \(total)�j")
                    }
                }
                if r.basePoints > 0 {
                    Text("��{�_: \(r.basePoints)").font(.footnote).foregroundColor(.secondary)
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

