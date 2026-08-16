# リスティングページの保守メモ

`Website/` のページは Scriban テンプレートで、GitHub Actions の
`Build Repo Listing`（`.github/workflows/build-listing.yml`）が
[package-list-action](https://github.com/vrchat-community/package-list-action) で
実データを流し込み、GitHub Pages へデプロイする。

このメモは**将来のバージョンアップ時に、どこを手で直す必要があるか**の書き置き。
検証の手順は `.claude/skills/listing-check/`、意匠・文言の規範は
`.claude/skills/tsukumodo-design/` にある。

## デプロイのされ方（重要）

| 変更の種類 | デプロイ |
|---|---|
| パッケージの新バージョンをリリース | `Build Release` 完了後に `Build Repo Listing` が**自動で**走る |
| `Website/` だけを変更して main にマージ | **自動では何も起きない。** Actions から `Build Repo Listing` を**手動実行**すること |

`build-listing.yml` に `push` トリガーは無い。「マージしたのに反映されない」ときは
まずこれを疑う。ワークフローはリポジトリ変数 `PACKAGE_NAME` を参照している。

## バージョンアップ時に手で直す場所

ページの大半はテンプレートで自動追従する（版数・パッケージ名・依存・URL）。
ただし以下は `Website/index.html` に**手書き**なので、実装が変わったら追従させること。

### チェック（守り札）を追加・変更したとき
- 守り札セクションに札を1枚追加する（見出し / 状況 / 深刻度バッジ /
  必要なら「Modular Avatar が必要」印 / `<details>`「見つけかたを読む」）。
  札は `Editor/Checks/*.cs` と1対1（現在10枚）。
- 「✓ 自動修正あり」バッジは **valueLabel（修正前後の予告）を持つチェック**に付ける
  （現在4枚: AnimatorLayerWeight / DescriptorDuplicate / MissingScript / Emission）。
- 深刻度の表示語は Unity 画面と同じ「エラー」「警告」のまま。

### 動作環境が変わったとき
- 「動作環境」節: Unity 2022.3以降 / VRChat SDK - Avatars 3.7.0以降 / MA任意。
- ヒーローのバッジ列: 無料 / MIT License / MA対応(任意) / Unity 2022.3〜。

### 利用統計の仕様が変わったとき
- 「利用統計のお約束」節の記録項目リスト。
  **`Packages/com.tsukumodo.avatar-omamori/README.md` と同期させること**
  （last_exported_at / opt_out / schema_version を落とした前科がある）。
- EditorPrefs（初回告知の既読フラグ）の説明も実装と突き合わせる。

### 「直すときも、ていねいに」節
- 修正履歴の寿命は「Unityを終了するまで」（FixHistoryStore が static のため）。
  ウィンドウ破棄で消える実装に変えたら文言も変える。

### 外部仕様が変わったとき
- ALCOM の記述（vcc:// 対応は v0.1.4 以降 / macOS は既定で有効 /
  Windows・Linux は設定画面で有効化）。一次情報は vrc-get の CHANGELOG-gui.md。
  **一次情報で確認できた範囲しか書かない。**
- カスタムドメインを設定したら `og:image` / `twitter:image` の絶対URL
  （現在 `https://tsukumodo.github.io/omamori/...`）を差し替える。

### プレビュー用フィクスチャ
- `tools/listing-preview/fixtures/listing.json` は**本番の package.json と同じ形**を保つ。
  任意項目（licensesUrl / author.url など）が本番で空なら、フィクスチャでも空にする。
  フィクスチャだけ豊かにすると、本番でだけ死ぬリンクが検出できない。

## してはいけないこと

- HTMLコメントの中にテンプレートタグの表記を書く（Scriban が実タグとして解釈し、
  ページが複製された事故がある。プレビューはこれを警告できない）。
- GitHub へのリンク・他商品の掲載・zip直リンク・ALCOMへの外部リンク。
- 空になりうる項目への `|| '#'` 型フォールバック（空なら href を外す）。
- `Website/` に公開したくないファイルを置く（**ディレクトリごと Pages に公開される**。
  このメモを `docs/` に置いているのもそのため）。
