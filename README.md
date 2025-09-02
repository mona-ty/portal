# 麻雀点数計算 iOS アプリ（SwiftUI）

iPhone 向けのリーチ麻雀 点数計算アプリのソース一式です。役は日本語の音声でトグルでき、ツモ/ロン、親/子、ドラ・裏ドラ・赤ドラ、本場・供託も反映します。Xcode で新規 iOS App を作成し、`MahjongScorerApp` 配下をプロジェクトに追加してビルドしてください。

## 主な機能
- 音声入力（日本語, Apple Speech）で役・状況・ドラを反映
- 役選択UI（門前/副露、ツモ/ロン、親/子）
- ドラ/赤ドラ/裏ドラのカウント
- 符の自動推定＋手動上書き
- 点数計算（満貫〜役満、数え役満、切り上げ満貫）、本場/供託の加算
- 計算内訳（翻/符/限界）表示

## 確定ルール（ご要望反映）
- 喰いタン: あり
- 赤ドラ: 3（採用数。UIでは実際の獲得枚数を入力）
- 裏ドラ: あり
- 切り上げ満貫: あり
- 数え役満: あり（13翻以上）
- ダブル役満: あり（四暗刻単騎/純正九蓮/大四喜）
- 特殊役: 流し満貫=満貫扱い／人和=役満扱い／十三不塔=役満扱い
- 半荘管理（オカ/ウマ/場管理/途中流局 など）: なし（未実装）

必要があれば、人和・十三不塔の格付けやダブル役満の対象は設定画面で切り替え可能に拡張できます。

## セットアップ（Xcode 15+）
1) Xcode: File > New > Project… > iOS > App
   - Interface: SwiftUI / Language: Swift
2) プロジェクトへ `MahjongScorerApp` フォルダの中身をドラッグ＆ドロップ（Copy items if needed ON）
3) `Info.plist` に以下のキーを追加
   - `NSSpeechRecognitionUsageDescription`（音声認識の利用理由）
   - `NSMicrophoneUsageDescription`（マイク利用の理由）
4) 実機でビルド（Speech は実機推奨）

## 音声コマンド例
- 基本: 「ツモ」「ロン」「親」「子」「門前」「副露」
- 1〜3翻: 「リーチ」「ダブルリーチ」「一発」「平和」「タンヤオ」「一盃口」「三色同順」「一気通貫」「対々和」「三暗刻」「三色同刻」「三槓子」「混全帯么九」「純全帯么九」「混老頭」「小三元」「混一色」「清一色」「七対子」
- 役満: 「人和」「国士無双」「四暗刻（単騎）」「大三元」「字一色」「緑一色」「清老頭」「小四喜」「大四喜」「四槓子」「九蓮宝燈（純正）」「天和」「地和」「十三不塔」
- 特殊: 「流し満貫」
- ドラ数: 「ドラ3」「赤ドラ2」「裏ドラ1」

## フォルダ構成
- `MahjongScorerApp/`
  - `MahjongScorerApp.swift` (@main)
  - `ContentView.swift`（UI）
  - `Models/`（`Yaku.swift`, `ScoreCalculator.swift`）
  - `ViewModels/`（`ScoringViewModel.swift`）
  - `Services/`（`SpeechRecognizer.swift`）
  - `Utils/`（`JapaneseNumberParser.swift`）

## 補足
- 形からの自動役判定は未対応（役の直接選択/音声）ですが、要望があればタイル入力モードの追加も可能。
- 今後の拡張: 設定画面（ルール切替）、履歴保存、Siri Shortcuts/ウィジェット など。
