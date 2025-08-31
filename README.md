# Bront (ブロント語ジェネレータ)

- 公開URL: https://mona-ty.github.io/Bront/
- 静的サイトはリポジトリ直下の `docs/` から配信されています。
- 生成ロジック/CLI は `buronto-generator/` に配置しています。

## 構成
- `docs/` … GitHub Pages 用の Web UI（favicon/OG メタ付き、ダークテーマ）
- `buronto-generator/` … Python 実装（CLI, データ, Web 用 HTML のソース）

## ローカルで Web を開く
ブラウザで `docs/index.html` を直接開くだけで動作します（依存なし）。

## Python 版の実行
```bash
python -m buronto.cli --count 5 --seed 42 --strength 0.7
```

## GitHub Pages 設定メモ
- Settings → Pages
- Source: Deploy from a branch
- Branch: `main`、Folder: `/docs`

