自動セットアップ（C/Windows）

概要
- FF14を通常プレイ中に潜水艦リストが一瞬でも表示されたら、自動でキャプチャ領域を推定して保存します。
- ネイティブCヘルパー `native/auto_setup.c` がウィンドウをキャプチャし、TesseractのTSV出力から「Rank/時間/分」を含む行群の矩形を検出します。

ビルド（MSVC）
```
cl /O2 native/auto_setup.c /Fe:native/auto_setup.exe
```
※ Visual Studio Build Tools もしくは Developer Command Prompt を使用。`tesseract.exe` が PATH にあること。

使い方
```
python -m ff14_submarines --auto-setup
```
- 最大5分間バックグラウンドで検出（20秒間隔）。検出されると `data/config.json` に保存して終了。
- 以降は `python -m ff14_submarines` で通常運用できます。

グローバルホットキー（任意）
- 監視実行中でも、次のフラグでホットキー（Ctrl+Alt+S）を有効化できます:
```
python -m ff14_submarines --hotkey
```
- プレイ中に潜水艦リストを開いた状態で Ctrl+Alt+S を押すと、
  ネイティブヘルパーで即時検出（失敗時は60秒の短時間ウォッチ）→領域を保存します。

仕組み（簡単）
1. FF14ウィンドウを検索 → `PrintWindow`/`BitBlt` でBMPキャプチャ
2. `tesseract <bmp> <base> -l jpn+eng --psm 6 tsv` を実行してTSVを取得
3. 「Rank/分/時間/数字」を含む語がある行のバウンディングボックスを統合 → ROI
4. ウィンドウ座標を画面座標へ変換して `config.json` に保存

注意事項
- フルスクリーン（独占モード）では `PrintWindow/BitBlt` が取得できない場合があります。ボーダーレス/ウィンドウ表示を推奨。
- Tesseractの日本語データ（`jpn`）が必要です。`tesseract.exe` をPATHに通してください。
- 初回は潜水艦リストを一度だけ開く必要があります（開きっぱなしにする必要はありません）。
