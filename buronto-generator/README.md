ブロント語ジェネレータ
=====================

ブロント語（2ch/FF11界隈のネタ文体）をそれっぽく真似た架空の発言を生成するシンプルなジェネレータです。コーパス学習ではなく、手書きのテンプレート＋語彙＋軽いノイズで雰囲気を出しています。

特徴
----
- テンプレートと語彙のランダム合成で自然なバリエーション
- ブロント語っぽい語尾（「だが？」等）、三点リーダ、カタカナ強調（「プロ（）」）などの風味を可変
- Python CLI と ブラウザだけで動くシンプルな Web デモを同梱

使い方（Python CLI）
--------------------
前提: Python 3.9+

実行例:

```
python -m buronto.cli --count 5 --seed 42 --strength 0.7
```

引数:
- `--count` 生成行数（デフォルト 5）
- `--seed` 乱数シード（同じシードで同じ結果）
- `--strength` ブロント語の風味強度 0..1（語尾や三点リーダ等の発生確率に影響）

ライブラリとして使う:

```python
from buronto import generate

lines = generate(n=3, seed=1, strength=0.8)
for s in lines:
    print(s)
```

Web デモ
--------
`web/index.html` をそのまま開けばブラウザ内で生成できます（オフライン・依存なし）。

構成
----
- `buronto/data.py` 語彙リスト
- `buronto/generator.py` 生成ロジック
- `buronto/cli.py` CLI エントリ
- `web/index.html` クライアントサイド実装

注意
----
- 本ツールはパロディです。実在の個人・集団を攻撃する表現は避けるよう、語彙は比較的マイルドにしてあります。


GitHub Pages
------------
このリポジトリは GitHub Pages で公開できるように、静的サイト用の docs/ ディレクトリに Web UI を複製しています。

手順:
1. GitHub 上のこのリポジトリの Settings を開く
2. Pages を選択
3. Source を「Deploy from a branch」に設定
4. Branch を main（または既定のブランチ）、Folder を /docs に設定して Save

これで https://<ユーザー名>.github.io/<リポジトリ名>/ で docs/index.html が公開されます。
Jekyll の影響を避けるため、docs/.nojekyll を同梱しています。
