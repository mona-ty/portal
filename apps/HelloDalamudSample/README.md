Hello Dalamud Sample

概要
- XIVSubmarinesReturn を参考にした最小の Dalamud プラグイン雛形です。
- チャットコマンド `/hds` と `/hello` を登録し、チャットにメッセージを表示します。

ビルド手順
1) Dalamud SDK のパスを `Local.props` で設定（例は `Local.props.example` 参照）
2) Release/x64 でビルド:
   `dotnet build apps\HelloDalamudSample\HelloDalamudSample.csproj -c Release -p:Platform=x64`

devPlugins での配置
- 出力先: `%AppData%\XIVLauncher\devPlugins\HelloDalamudSample`
- `HelloDalamudSample.dll` と埋め込み `manifest.json` が利用されます。

コマンド
- `/hello` : 挨拶を出力
- `/hds`   : ヘルプを表示

