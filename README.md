Portal

このリポジトリは、個別のアプリケーション（ポリレポ）への入口（ポータル）です。実装コードは各リポジトリに分割・移管済みで、本リポジトリには主にドキュメントと補助スクリプトが置かれます。

リポジトリ一覧
- FF14 Submarines（OCR + Calendar ツール）: https://github.com/mona-ty/ff14-submarines
- ToDo アプリ（Web）: https://github.com/mona-ty/todo-app
- Buronto Generator（和文スタイル考慮のテキスト生成 + Web）: https://github.com/mona-ty/buronto-generator

含まれるもの（抜粋）
- `docs/` ドキュメント類
- `scripts/` 補助スクリプト（分割・公開など）

分割スクリプトの使い方（参考）
- 実行: `pwsh scripts/split_repos.ps1 -Visibility public [-Owner <github-user>] [-Force]`
- 概要: `git subtree split` と `gh repo create` を使って、履歴付きで各フォルダを独立リポにプッシュします。`-Force` でリモート `main` を強制更新します。

備考
- 本リポはコードの集約先ではなく、案内・メタ情報の保管場所として運用します。
