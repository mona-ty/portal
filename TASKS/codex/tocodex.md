以下は **Codex CLI** に渡すための、複数キャラクター対応（最大10）の\*\*実装計画（日本語）\*\*です。
Codexは `*** Begin Patch` 形式のパッチを理解し、また通常の「手順付き仕様」も実行計画として扱えます。計画の最後に、この仕様を `docs/` に追加するためのパッチも同梱しています（必要ならそのまま `apply_patch` で適用してください）。Codex/CLIのパッチ形式は一般に `*** Begin Patch`/`*** End Patch` の統一 diff を想定します。([OpenAI Developers][1], [Fabian Hertwig's Blog][2], [GitHub][3])

> キー設計の前提
> Dalamudの `IClientState` には **`LocalContentId : ulong`** があり、ログイン中のローカルキャラクターを一意に識別できます。これをキャラクタープロファイルの主キーに採用します。([dalamud.dev][4])

---

## ✅ 目的

* 現状「潜水艦情報」が**1キャラクター分のみ**保存される制約を撤廃し、
  **最大10キャラクター**分を**独立して保存・切替**できるようにする。
* 例）

  * 1キャラ目: `server: Chocobo` / `fc: freecompany1` / `charactername: Name Hoge`
  * 2キャラ目: `server: Shinryu` / `fc: freecompany2` / `charactername: Namae Dayo`

---

## 🧩 全体方針（設計）

1. **キャラクタープロファイル導入**

   * 新モデル `CharacterProfile`（新規 or `Models.cs` に追加）を定義し、**1キャラ=1プロファイル**で潜水艦データ一式を内包。
   * 主キー：`ContentId (ulong)` = `IClientState.LocalContentId`。
     併せて表示用に `CharacterName`, `WorldName`, `FreeCompanyName`, `LastSeenUtc` 等を保持。([dalamud.dev][4])
   * 既存の**潜水艦保存用モデル**（現在 `Configuration.cs`/`Models.cs` で単一キャラ前提になっている入れ子）を**そのままプロファイル内に移設**する（※型名は現行を流用）。

2. **Configuration の多重化**

   * `Configuration` に以下を追加し、**複数プロファイル**を扱えるようにする：

     * `int ConfigVersion`（マイグレーション管理）
     * `List<CharacterProfile> Profiles`（上限10）
     * `ulong? ActiveContentId`（UIで選択中 or 自動検出中のプロファイル）
   * **管理上限10**：`Profiles.Count >= 10` の場合は新規追加を抑止しUIで警告表示。

3. **既存データの移行（V1→V2）**

   * 起動時に `ConfigVersion < 2` なら、**現行の単一キャラ用保存領域**を `CharacterProfile` 1件に**包んで** `Profiles` へ移行。
   * `ContentId` は取得できる場合 `LocalContentId`、取得不能時は `Hash(name@world)` の**暫定ID**に格納（次ログインで更新）。
   * `ConfigVersion = 2` に更新して保存。

4. **アクティブプロファイルの解決**

   * プラグイン起動/ログイン時：

     * `IClientState.LocalContentId` と一致するプロファイルがあればそれを**Active**に。
     * 無ければ `Profiles.Count < 10` なら**自動作成**して Active に、超過なら\*\*既定（先頭）\*\*をActiveに。
   * UIのプルダウンで**任意選択**も可（手動スイッチ）。

5. **UI 変更（最小）**

   * `Plugin.UI.cs` または `Widgets.cs` に**共通セレクタ** `DrawProfileSelector()` を追加し、
     `OverviewTab.cs`, `AlarmTab.cs`, `SnapshotTable.cs` など**潜水艦表示/操作系タブ**の先頭で呼び出し。
   * セレクタ項目表示：`{CharacterName} @ {WorldName} — {FreeCompanyName}`。
   * 右側に `＋（現在のキャラを追加）` / `🗑（削除）` / `✎（名称修正※表示項目のみ）` / 上限警告。
   * **削除**は安全のため**確認ダイアログ**を挟む。

6. **書き込み経路のスコープ化**

   * `Extractors.cs` / `SectorCommands.cs` / `OverviewTab.cs` 等で、**保存・参照対象を常に ActiveProfile 以下**へ変更：

     * 例）`config.Submarines` → `config.GetActiveProfile().Submarines`
   * **受信した潜水艦の進捗・ETAs など**は全て ActiveProfile 配下へ集約。

7. **イベント連動**

   * `Plugin.cs` で `IClientState.Login`/`Logout` を購読し、
     ログイン時に **プロファイル自動作成/切替**、`LastSeenUtc` 更新、`WorldName`/`FreeCompanyName` 自動更新。

8. **エッジケース**

   * **ワールド移転/FC変更/改名**：ログイン時に `WorldName`/`FreeCompanyName`/`CharacterName` を**上書き更新**（`ContentId` は変わらないため同一プロファイル扱い）。
   * **`LocalContentId` 取得不可**（ログアウト画面等）：UIは手動選択のみ許容。
   * **10件超過**時：自動追加をしない（UIで説明）／手動で削除してから追加。

---

## 📁 影響範囲（ファイル単位の作業計画）

> 実際の型名/プロパティ名は**現行コードに合わせて**調整してください。ここでは方針と差分の要点を列挙します。

* **`Models.cs`（または新規 `CharacterProfile.cs`）**

  * `public sealed class CharacterProfile` を追加。
    必須プロパティ：`ulong ContentId`, `string CharacterName`, `string WorldName`, `string FreeCompanyName`, `DateTime LastSeenUtc`, `ExistingSubmarineRootType Submarines`（現行の潜水艦保存ルート型を内包）。
* **`Configuration.cs`**

  * 新規：`int ConfigVersion = 1;`（既存がなければ）→ 実装後は `= 2`。
  * 新規：`List<CharacterProfile> Profiles { get; set; } = new();`
  * 新規：`ulong? ActiveContentId { get; set; }`
  * Helper：`CharacterProfile? FindProfile(ulong cid)`, `CharacterProfile GetOrCreateProfileFor(ulong cid, Func<CharacterProfile> factory)`
  * **Migration**：`MigrateToV2IfNeeded()` を `Plugin` 初期化時に呼ぶ。
  * **上限**：`Profiles.Count >= 10` で追加拒否。
* **`Plugin.cs`**

  * `IClientState` 注入を確認し、`Login` ハンドラで `LocalContentId` を解決、`GetOrCreateProfileFor(cid, ...)` により Active を確定。`Save()`。
* **`Plugin.UI.cs` / `Widgets.cs`**

  * `DrawProfileSelector(ref Configuration config, IClientState clientState)` を追加し、
    各タブ（`OverviewTab.cs`, `AlarmTab.cs`, `SnapshotTable.cs` など）の冒頭で呼ぶ。
* **`Extractors.cs` / `SectorCommands.cs` / `OverviewTab.cs` / `AlarmTab.cs` / `SnapshotTable.cs`**

  * **参照/保存先**を `config.GetActiveProfile().Submarines` 系へ差し替え。
  * コマンドで「現在のキャラ向け」の動きを明確化（ActiveProfile前提）。
* **`DebugTab.cs`**

  * 現在の `LocalContentId`、ActiveProfile、`Profiles.Count` を表示（動作確認用）。

---

## 🔐 データ互換（移行設計）

* 旧構造が `Configuration` 直下に `Submarines`（仮）等で単一保存されている想定：

  1. `ConfigVersion < 2` なら、新規 `CharacterProfile` を1件生成し**旧データの参照を差し替える**（深いコピー不要・参照移設でOKなら参照移設、NGならシリアライズ/デシリアライズで複製）。
  2. `ContentId` は `clientState.IsLoggedIn ? LocalContentId : Hash(旧表示名)` を暫定設定。
  3. `Profiles.Add(profile); ActiveContentId = profile.ContentId; ConfigVersion = 2; Save();`

---

## 🧪 受け入れ条件（手動テスト）

1. **移行**：旧版設定で起動 → V2 に自動移行し、UIで1つ目のプロファイルが選択されている。潜水艦データは保持。
2. **自動追加**：別キャラでログイン → `Profiles` に自動で2件目が追加され Active 切替。以後、各キャラで独立してデータが保持される。
3. **上限**：11キャラ目をログイン → 自動追加されず警告表示。手動削除→追加で回避可能。
4. **変更**：キャラの名前/FC/ワールドが変わっても **ContentId が同じ**なら同一プロファイルでメタ情報のみ更新。
5. **UI**：セレクタから任意のプロファイルを選ぶと、表示・操作対象が切り替わる。
6. **保存**：ゲーム再起動後も各キャラの潜水艦データが独立に復元される。

---

## ⚠️ 注意・補足

* `LocalContentId` は**ローカルキャラの一意識別子**として適切です。本実装では**自分のキャラのみ**を対象に扱い、他者のID収集は行いません。([dalamud.dev][4])
* Codex CLI は、**タスク仕様のMarkdown**や**パッチ形式**の両方を扱えます。今回、実装前に仕様を `docs/` に落とすためのパッチも併記します。([OpenAI Developers][1], [Fabian Hertwig's Blog][2])

---

# ✅ Codex への具体的な指示（そのまま貼り付けOK）

以下の「開発手順」を順に実行してください。必要に応じてコード検索し、既存型名に合わせて置き換えてください。

1. **モデル追加**

   * `Models.cs`（または新規 `CharacterProfile.cs`）に `CharacterProfile` を追加。

     * プロパティ：`ContentId (ulong)`, `CharacterName`, `WorldName`, `FreeCompanyName`, `LastSeenUtc`, `Submarines`（既存の潜水艦ルート型）。
2. **設定スキーマ更新**

   * `Configuration.cs` に `ConfigVersion`, `List<CharacterProfile> Profiles`, `ulong? ActiveContentId` を追加。
   * `FindProfile`, `GetOrCreateProfileFor`、`GetActiveProfile`（`ActiveContentId` → 該当 or 先頭）を実装。
   * `Save()` は既存の `PluginInterface.SavePluginConfig(this)` を利用（現行仕様に追従）。
3. **マイグレーション**

   * `Plugin` 初期化時に `MigrateToV2IfNeeded()` を呼び、旧単一データを `CharacterProfile` 1件へ移行。
4. **イベント購読**

   * `Plugin.cs` で `IClientState.Login` を購読し、`LocalContentId` から ActiveProfile を確定/作成。
     `CharacterName/WorldName/FreeCompanyName/LastSeenUtc` を更新し `Save()`。
5. **UI：プロファイルセレクタ**

   * `Widgets.cs`（または `Plugin.UI.cs`）に `DrawProfileSelector()` を新設。

     * Comboで `Name @ World — FC` を一覧。
     * `＋` ボタン：`LocalContentId` をカレントに追加（上限チェック）。
     * `🗑`：選択プロファイル削除（要確認ダイアログ）。
     * `✎`：表示名の手動編集が必要なら実装（任意）。
     * 上限到達時は説明テキスト表示。
   * `OverviewTab.cs`, `AlarmTab.cs`, `SnapshotTable.cs` などの先頭で `DrawProfileSelector()` を呼ぶ。
6. **保存先のスコープ化**

   * `Extractors.cs` / `SectorCommands.cs` / 各タブでの読み書きを `config.GetActiveProfile().Submarines` に差し替える。
7. **バリデーションとログ**

   * 追加時の上限制御、マイグレーション時のログ、想定外データ時の防衛コードを追加。
8. **動作確認**

   * 上記「受け入れ条件」を満たすまで修正＆テスト。

---

## （任意）仕様書をリポジトリに追加するパッチ

> まずは仕様を `docs/` に落としてから実装を進めたい場合にどうぞ。Codexの `apply_patch` で適用できます。([Fabian Hertwig's Blog][2])

```
*** Begin Patch
*** Add File: docs/multi-character-profiles.md
+# Multi-Character Submarine Profiles (up to 10)
+
+## Goal
+Store submarine data per character (max 10) and allow switching between profiles.
+
+## Key
+- Primary key per character: `IClientState.LocalContentId : ulong`.
+- Show fields: `CharacterName`, `WorldName`, `FreeCompanyName`, `LastSeenUtc`.
+
+## Schema Changes
+- Add `CharacterProfile` model embedding the existing submarine root type.
+- In `Configuration`: 
+  - `int ConfigVersion = 2`
+  - `List<CharacterProfile> Profiles`
+  - `ulong? ActiveContentId`
+  - Helpers: `FindProfile`, `GetOrCreateProfileFor`, `GetActiveProfile`.
+
+## Migration (V1 -> V2)
+If old single-character storage exists:
+1) Wrap it into one `CharacterProfile`.
+2) Use `LocalContentId` if available, otherwise a temporary hash of `name@world`.
+3) Set `ActiveContentId` and persist.
+
+## UI
+- Add a shared selector (`DrawProfileSelector`) used by overview/alarm/snapshot tabs.
+- Show `Name @ World — FC`, with add/remove/rename controls and a "max 10" guard.
+
+## Write Path
+- All reads/writes go through `config.GetActiveProfile().Submarines`.
+
+## Edge Cases
+- Name/World/FC changes update metadata, profile key remains `ContentId`.
+- If not logged in (no `LocalContentId`), only manual profile selection is available.
+- Do not auto-add beyond 10.
+
+## Acceptance
+See project README/testing doc; verify migration, auto-add on login, 10-cap guard,
+and independent persistence per character.
+
*** End Patch
```

---

### 参考リンク

* Codex CLI の概要（CLIがリポジトリを読み、パッチやコマンドで編集）([OpenAI Developers][1])
* Codex系の\*\*パッチ形式（`*** Begin Patch`/`*** Update File`）\*\*の解説・実例 ([Fabian Hertwig's Blog][2], [GitHub][3])
* Dalamud `IClientState` の `LocalContentId` プロパティ（キャラ一意識別）([dalamud.dev][4])

---

必要であれば、この計画に合わせて**具体的な差分テンプレート**（`Configuration.cs`/`Models.cs` に入れるコード雛形や UI コンボのサンプル実装）もすぐ用意します。

[1]: https://developers.openai.com/codex/cli/?utm_source=chatgpt.com "Codex CLI"
[2]: https://fabianhertwig.com/blog/coding-assistants-file-edits/?utm_source=chatgpt.com "How AI Assistants Make Precise Edits to Your Files"
[3]: https://github.com/openai/codex/issues/3031?utm_source=chatgpt.com "codex-cli 0.27.0 - You've hit your usage limit, try again in 4 ..."
[4]: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState/ "Interface IClientState | Dalamud"
