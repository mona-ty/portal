結論：**とても良くまとまっています**が、運用で再発しやすい箇所に関して**5つの“抜け/曖昧”**があります。以下の**追記パッチ**を計画書にそのまま貼れる形で用意しました（Diff ではなく「追補テキスト」）。根拠として、あなたの計画書・実測ログ・JSON出力と、最新の Dalamud v13 API 仕様を引用します。    ([Dalamud][1])

---

## まず「抜け/曖昧」だけ要約

1. **ワークショップ外での無駄スキャン抑止（Territory/Condition ゲート）**
   　→ 計画に明記なし。`IClientState.TerritoryType`（工房のテリトリー）や Addon 可視性で**イベント駆動＋条件式**に限定しましょう。([Dalamud][2])

2. **“逆順（reversed）”候補の扱いが明記されていない**
   　→ 既存の仕様サマリでは reversed を考慮していますが、今回の計画本文には明記されていません。**tail を含む forward / reverse**の両方を必須条件として規定してください。

3. **降格防止（TTL）を「セッション間」でも維持**
   　→ TTL/Confidence は明記済みですが、**プロセス再起動やゲーム再起動**で消えると再発します。**Character|World|Slot** をキーに**永続キャッシュ**（プロファイルに保存）を追加してください。Notion 連携で採用している**安定キー生成ロジック**（`BuildStableId` の考え方）を流用するのが簡便です。

4. **AddonLifecycle の “RequestedUpdate” 系イベントを監視対象に追加**
   　→ PostSetup/Refresh は計画に入っていますが、**Pre/PostRequestedUpdate**では `ArrayData` 更新のタイミングで **数値配列（AtkValue/ArrayData）**を直接読める場合があります。これは**スキャンより低コスト・高精度**なので「第一候補」に昇格させる価値があります。([Dalamud][1])

5. **採用スコアに“妥当値（rank/eta）検査”を含める旨の明文化**
   　→ 計画のリスク欄に“rank/eta妥当性で抑制”とありますが、**スコア計算式**として明示した方が安全です（tail一致強度 > 長さ > 近傍性 > 妥当値）。

> 参考：現状の症状
>
> * “**15,10**” といった**末尾2点だけ**が観測され続ける時間帯が長く、`memscan`も候補0が多発。完全列が出た直後に\*\*\[15,10]へ降格\*\*する事例がログで確認できます。**採用規則の厳格化＋TTL永続化＋イベント駆動**で解決可能です。 &#x20;

---

## 計画書への「追記パッチ」（そのままコピー可）

### A. 取得トリガーと条件（Territory/Condition/Addon）

* **EnableTerritoryGate（新）**: 既定 `true`。`IClientState.TerritoryType` が工房（Housing Workshop）に一致する場合のみメモリ取得処理を起動。([Dalamud][2])
* **EnableAddonGate（新）**: 既定 `true`。`IGameGui.GetAddonByName(...)` で対象アドオンが**ロード済みかつ可視**の場合のみ、以下のキャプチャフェーズを実行（アドオン名は設定で複数候補・部分一致を許容）。([Dalamud][3])
* **AddonLifecycle 対応イベント（拡張）**:

  1. **PostSetup/PostRefresh**（既存）→ 1–2F 遅延でキャプチャ
  2. **PreRequestedUpdate/PostRequestedUpdate**（**追加**）→ **ArrayData**（数値配列）を**第一候補**として解析し、成功ならスキャナをスキップ。([Dalamud][1])

### B. スキャナ仕様の明確化（reversed・妥当値）

* **Tail 包含条件**: forward **または reversed** の**連続部分列**または**末尾一致**を**必須**。候補は stride=1/2/4、位相ずらし（2byte上位、4byteの第2/第3バイト等）ごとに探索。
* **スコア関数（明文化）**:
  `Score = (Tail一致強度 × W1) + (長さ × W2) + (近傍性 × W3) + (妥当値 × W4)`

  * 妥当値: `1 <= sectorId <= 255`、`rank 1..130`、`ETA/Duration` のレンジチェックで部分加点。
* **段階フォールバック順**: ArrayData直読 → ±Window → full-struct → 位相 → **minLen=2（可視化のみ）**。失敗時でも採用は **TTL内のLastGoodRouteを保持**。

### C. 降格防止の**永続**キャッシュ

* **LastGoodRouteStore（新）**: `Character|World|Slot` をキーに**ディスク保存**。
* **Policy**: `Confidence=Full` は **TTL（既定12h）内は降格不可**（プロセス再起動後も保持）。TTL 超過後は `Partial > Tail` のみで上書き可。
* **Key 生成**: Notion の `BuildStableId` と同等の安定キー指針を参照（ただしハッシュ化は不要。生キーで OK）。

### D. ログ／診断の追加

* **memscan 出力**に `phase=array|window|full|phaseN`、`reversed=true/false` を明記。
* **memdump** はアドオン可視時のみ許可（事故防止）。
* 採用ログ：`adopted=cache/mem/array, reason=score(ttl/confidence), len, reversed, off, stride, phase`。

### E. 設定キー（追加）

* `EnableTerritoryGate=true`, `EnableAddonGate=true`
* `AdoptCachePersist=true`（LastGoodRouteの永続化を有効化）
* `AdoptCacheTtlHours=12`（既存 TTL を永続キャッシュにも適用）
* `PreferArrayDataFirst=true`（RequestedUpdateの ArrayData を最優先）

> これらを\*\*「改修方針」「設定/ログ」「実装ステップ」「受け入れ条件」\*\*の各節に差し込めば、計画の完成度が上がります。

---

## 根拠（あなたのログ/出力から）

* **末尾2点（15,10）しか読めない**時間帯が長い：`route: off=0x42,stride=1 -> [15,10]`、`memscan: candidates=0` の連続。→ **イベント駆動＋永続キャッシュ**が必要。&#x20;
* **短時間だけフル列を検出**：`off=0x1C/0x22/0x48` で full=\[…] を連続検出→**この瞬間を確実に捕まえられるイベント**（PostSetup/Refresh/RequestedUpdate）が有効。
* **一度成功後に降格**：`cache=[108,51,36,1,80]` を保持しているのに `final=[15,10]` を採用。→ **降格禁止（TTL）**の**永続**化が必要。
* **JSON ではフル航路が正常出力される瞬間あり**：`RouteKey` が 3–5 点、`RouteShort` も生成。→ **ロジック自体は成立**、**タイミングと採用規則**が課題。

---

## Dalamud v13 API の**再確認**（効率的な取得経路）

* **AddonLifecycle**：`PostSetup/Refresh` だけでなく `Pre/PostRequestedUpdate` も監視可能。\*\*UIへ入る数値配列（ArrayData）\*\*を扱えるため、**バイト走査より信頼性が高い**ケースがあります。([Dalamud][1])
* **IGameGui**（v13）: `GetAddonByName` は **AtkUnitBasePtr**、`GetAgentById/FindAgentInterface` は **AgentInterfacePtr** を返すよう変更。**安定アンカー**の取得・可視状態の確認・近傍アドレス探索の起点にできます。([Dalamud][3])
* **BaseAddressResolver / SigScanner**：**シグネチャ解決**による Agent/Manager 基点の確保に使用。パッチ耐性向上。([Dalamud][4])
* **公開プラグインの状況**：SubmarineTracker はルート/時間管理の代表例。**UIに依存しない時間管理**の設計や通知周りの参考になります（実装詳細の直接流用は不可）。([GitHub][5])
* **Goatcorp のプラグイン一覧**：周辺プラグインの UI/イベント活用パターンの調査に。([XIVLauncher | Custom launcher for FFXIV][6])

---

## 受け入れ条件（追記）

* \[追加] **工房テリトリー内 & 対象アドオン可視**のときのみフルキャプチャの各フェーズを実行。([Dalamud][2])
* \[追加] **RequestedUpdate（ArrayData）直読での復元が成功**した場合、**構造体スキャンはスキップ**し、採用は `source=array` とする。([Dalamud][1])
* \[追加] **永続キャッシュ**が有効で、**TTL 内は降格しない**（再起動後も同様）。

---

### これで「漏れ」は解消できますか？

* **はい**。上の5点を追記すれば、

  * ① **読むべき“瞬間”を確実に押さえ**（AddonLifecycle+ArrayData）、
  * ② **見つかったときに確実に勝ち採用**し（採用スコア明文化）、
  * ③ **見つからない時間帯でも降格せず安定**（永続TTL）、
  * ④ **無駄スキャンを抑止**（Territory/Addon ゲート）
    という観点が揃い、ログで再現していた\*\*「完全復元→数分後に15,10へ後退」\*\*の流れを止められます。

必要であれば、この追記を**PR テンプレ（チェックリスト項番付き）**や**Issue タスク分解**に整形してお渡しします。

[1]: https://dalamud.dev/api/Dalamud.Game.Addon.Lifecycle/Enums/AddonEvent/?utm_source=chatgpt.com "Enum AddonEvent | Dalamud"
[2]: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState/?utm_source=chatgpt.com "Interface IClientState"
[3]: https://dalamud.dev/versions/v13/?utm_source=chatgpt.com "What's New in Dalamud v13"
[4]: https://dalamud.dev/api/api13/Dalamud.Game/Classes/BaseAddressResolver?utm_source=chatgpt.com "Class BaseAddressResolver | Dalamud"
[5]: https://github.com/Infiziert90/SubmarineTracker?utm_source=chatgpt.com "Infiziert90/SubmarineTracker: Track and Build"
[6]: https://goatcorp.github.io/DalamudPlugins/plugins.html?utm_source=chatgpt.com "Neat Plugins | DalamudPlugins - XIVLauncher"
