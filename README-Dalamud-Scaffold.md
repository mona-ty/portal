Dalamud Plugin テンプレ生成（Windows）

概要
- goatcorp/SamplePlugin を元に、任意名のプラグイン雛形を1コマンドで生成します。
- .NET 8 SDK と Git が必要です。OS は Windows 前提。

使い方
- 新規生成
  - `just new MyAwesomePlugin`
  - 生成物構成
    - `MyAwesomePlugin.sln`
    - `MyAwesomePlugin/`（C#プロジェクト）
    - `Data/`（アセット）
    - `MyAwesomePlugin/justfile`（ビルド/Zip）

- ビルド/Zip（生成後）
  - `cd MyAwesomePlugin`
  - `just build-debug`（Debug）/ `just build-release`（Release）
  - `just zip-release`（配布Zip作成）

ゲーム内での有効化
- `/xlsettings` → Experimental → Dev Plugin Locations に DLL のフルパス追加
- `/xlplugins` → Dev Tools → Installed Dev Plugins から有効化

カスタマイズ
- `MyAwesomePlugin.json` の `Author/Name/Punchline/Description` を編集
- `Plugin.cs` のスラッシュコマンドや UI タイトルを編集

注意
- 生成物は AGPL-3.0 ライセンスが含まれます（SamplePlugin 準拠）。公開時は適宜変更してください。

