AIコードレビュー用サマリ（XIV Submarines Return）

ステータス要約
- 対応完了: Discord通知のETA（日付＋時刻）追加、Discord通知からスロット番号を除去、設定UIの自動リサイズ化、ルート復元ロジックの強化（メモリ＋キャッシュ＋UIフォールバック）。
- 互換性: 既存JSONスキーマは維持（ExtraにEtaLocalFull追加、RouteShort生成は上書き抑止）。
- ビルド: 成功（0エラー/11警告）。

今回の目的と結果
- 目的1: Discordに「何月何日の何時」に完了するETAを出す
  - 実装: EtaFormatterでExtra["EtaLocalFull"]を付与し、DiscordNotifierがこれを優先して表示。Embed/テキスト両方対応。
  - 追加: Discordのフィールド名（name）からスロットを除去。行テキストもスロット削除。
- 目的2: ルートがO>J等の短縮で止まる不具合の解消
  - 実装: メモリ値（1～2点しか読めない時）に対し、スロット別キャッシュ（3点以上のフルルート）と「連続部分列／逆順部分列」一致でフルを復元。
  - 追加: 工房UI（SelectString）が見える場合はUI抽出でルートを補完（3点以上が得られたらキャッシュ更新）。
  - 注意: 初回にキャッシュが無い・UIも読めない状況では誤推測を避け、短縮のまま表示（次回以降に学習）。

変更ファイル一覧（要点）
- apps/XIVSubmarinesReturn/src/Configuration.cs
  - 追加: `AutoResizeWindow`（bool、既定true）
  - 追加: `LastRouteBySlot: Dictionary<int,string>`（スロット→前回フルルート）
- apps/XIVSubmarinesReturn/src/Plugin.UI.cs
  - `ImGui.SetNextWindowSize(..., FirstUseEver)` に変更、`AlwaysAutoResize` を設定で付与
- apps/XIVSubmarinesReturn/src/Services/EtaFormatter.cs
  - 追加: `EtaLocalFull`（"yyyy/M/d HH:mm"）
  - RouteShortは既存値があれば上書きしない
- apps/XIVSubmarinesReturn/src/Services/DiscordNotifier.cs
  - Embedの`name`を「名前のみ」に変更（スロット除去）
  - 行生成`BuildSnapshotLine2`を使用（フルETA、残り分、RouteShort）。表示もスロット除去
  - アラーム通知文もフルETA＋スロット除去に更新
- apps/XIVSubmarinesReturn/src/Plugin.cs
  - メモリ→数列化、キャッシュ（3点以上）と部分列/逆順部分列で突合し採用判定
  - SelectString UI抽出フォールバックを追加（短い場合のみ）
  - 追加関数: `BuildRouteKeyFromNumbers`, `BuildRouteShortFromNumbers`, `ReverseCopy`, `ContainsContiguousSubsequence`, `TryGetCachedNumbers`, `SaveCache`
  - 既存`IsSuffix`の重複を整理

変更コード抜粋
```
// Discord: Embedフィールド（スロット除去）
name = (it.Name ?? string.Empty),
value = BuildSnapshotLine2(it),

// Discord: 行出力（スロット除去 + フルETA）
return $"[Sub] {it.Name} {eta} (残 {rem}) {rt}".Trim();

// Discord: アラーム文（フルETA + スロット除去）
var msg = $"[Sub Alarm] {rec.Name} ETA {etaFull} (残 {leadMinutes}分) {routeText}";

// EtaFormatter: フルETA付与 & RouteShort上書き抑止
it.Extra["EtaLocalFull"] = eta.ToString("yyyy/M/d HH:mm");
if (!it.Extra.TryGetValue("RouteShort", out var existing) || string.IsNullOrWhiteSpace(existing))
    it.Extra["RouteShort"] = ShortRoute(it.RouteKey);

// Plugin.cs: ルート復元の中核（要約）
var memNums = pts.Select(b => (int)b).ToList();
var cached  = TryGetCachedNumbers(slot);
List<int> routeNumbers; bool adoptedCache=false; string reason="mem";
if (memNums.Count >= 3) { routeNumbers = memNums; SaveCache(slot, routeNumbers); }
else if (memNums.Count > 0 && cached?.Count >= 3) {
  var memR = ReverseCopy(memNums);
  if (ContainsContiguousSubsequence(cached, memNums)) { routeNumbers=cached; adoptedCache=true; reason="subseq"; }
  else if (ContainsContiguousSubsequence(cached, memR)) { routeNumbers=cached; adoptedCache=true; reason="reverse-subseq"; }
  else routeNumbers = memNums;
}
else if (memNums.Count == 0 && cached?.Count >= 3) { routeNumbers=cached; adoptedCache=true; reason="none"; }
else routeNumbers = memNums; // 短いまま

rec.RouteKey = BuildRouteKeyFromNumbers(routeNumbers);
rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(routeNumbers);

// 短い場合のUIフォールバック（SelectString）
if (routeNumbers.Count < 3 && Config.UseSelectStringExtraction) {
  if (TryCaptureFromAddon("SelectString", out var snapUi)) {
    var k = snapUi.Items.FirstOrDefault(x => x.Name?.Trim()==rec.Name?.Trim())?.RouteKey;
    var numsUi = string.IsNullOrWhiteSpace(k) ? null : ParseRouteNumbers(k);
    if (numsUi?.Count >= 3) {
      rec.RouteKey = BuildRouteKeyFromNumbers(numsUi);
      rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(numsUi);
      SaveCache(slot, numsUi); routeNumbers = numsUi; adoptedCache = true; reason = "ui";
    }
  }
}

// ログ
XsrDebug.Log(Config, $"S{slot} route mem=[{string.Join(",", memNums)}], cache=[{(cached==null?"-":string.Join(",", cached))}], " +
             $"adopted={(adoptedCache?"cache":"mem")}, reason={reason}, final=[{string.Join(",", routeNumbers)}]");
```

ビルド結果（直近）
- 成功（0エラー/11警告）
- 警告内訳（主なもの）
  - CS8604/CS8601/CS8602: Null許容関連（NotionClient/SectorResolver/Plugin）
  - CS0169: 未使用フィールド（現状は解消済み）
  - CS0414: 値割当のみ（_sortField/_sortAsc）

検証方法（手動）
- Discord
  - Embed/テキスト通知で、name・本文からスロットが消えていること
  - ETAが `yyyy/M/d HH:mm` 形式で表示されること
- ルート
  - 工房UIを開いた状態で「メモリから取得」。
  - メモリが [15,10] 等の短縮でも、前回キャッシュ or UI抽出で3点以上が得られれば M>R>O>J>Z を表示
  - XsrDebug ログで `mem, cache, adopted, reason, final` を確認
- UI
  - 設定ウィンドウの縦幅が内容に応じて変動（`AlwaysAutoResize`）

既知課題/次の改善候補
- 依然として初回取得で「キャッシュ無し・UIが見えない」タイミングでは短縮のまま（安全側）。
  - 対案: ルートキャッシュのキーを「スロット＋名前」に拡張、別Addon（Map/Result）からの抽出も併用。
- ビルド時警告の解消（別タスクで対応可）。

補足（ログ位置）
- 抽出ログ: `%AppData%/XIVSubmarinesReturn/bridge/extract_log.txt`
- 追加デバッグ: XsrDebug.Log（設定のDebugLoggingが有効な場合）
