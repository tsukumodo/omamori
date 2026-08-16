# listing-preview

`Website/index.html` と `Website/app.js` は Scriban テンプレートで、本番では
GitHub Actions 上の `vrchat-community/package-list-action` が描画してから
GitHub Pages にデプロイされる。つまり**本番デプロイするまでページの見た目を
確認できない**ため、ダミーデータで手元に描画するためのツール。

## 使い方

```sh
# Website/ を _preview/ にダミーデータで描画する
node tools/listing-preview/preview.mjs

# 描画 + http://localhost:4173 で配信（ブラウザでそのまま確認できる）
node tools/listing-preview/preview.mjs --serve

# ポートを変える場合
node tools/listing-preview/preview.mjs --serve --port 5000
```

`_preview/` はリポジトリ直下に生成される（`.gitignore` 済み）。実行のたびに
中身は作り直される。

## 仕組み

- `Website/` 配下を丸ごと `_preview/` にコピーする
- そのうち `index.html` と `app.js` の2ファイルだけは Scriban 風の
  プレースホルダ（`{{ x.y }}` / `{{~ for x in y ~}}...{{~ end ~}}` /
  `{{ if x; x; end; }}`）を `tools/listing-preview/fixtures/listing.json`
  の内容で置換してから出力する
- それ以外（画像・favicon・`vendor/` の同梱ライブラリなど）はそのままコピーする
- フィクスチャに無いフィールドなど、解決できないプレースホルダに出会うと
  **stderr に警告を出し**、出力にも `{{ ... }}` の形のまま残す。
  黙って空文字にはしない。Scriban テンプレートの契約
  （`Website/index.html` / `app.js` に埋め込まれた `{{ }}` の形）を
  壊す変更をしてしまったときに気づけることが、このツールの一番の価値なので
- Node.js 22 標準ライブラリのみで動く。npm 依存は追加していない
  （`package.json` も置いていない）

## 制限事項

これは本物の Scriban 実装ではなく、このリポジトリの2ファイルで実際に使われて
いる構文のみをカバーした簡易実装。`package.Versions` のような、
DESIGN_SPEC.md で「存在未確認」とされているフィールドには対応していない。
