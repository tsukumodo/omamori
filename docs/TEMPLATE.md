# このリポジトリを新しいツールの雛形にする

つくも堂の次のツールを作るとき、このリポジトリの構成
（VPMパッケージ + Scriban リスティング + プレビュー機構 + CI）を
そのまま出発点にできる。

## 推奨: GitHub の Template repository にする

1. GitHub の **Settings → General → Template repository** にチェックを入れる（1回だけ）。
2. 新ツールを作るときは、リポジトリ作成画面で **「Use this template」** からこのリポを選ぶ。

fork ではなく template を使う理由: 履歴・Issue・PR を引き継がない
クリーンな複製が得られ、private のまま作れて、fork 特有の制約
（同一アカウントで1つまで、ネットワーク表示）も付かない。

共通部分を抜き出した専用テンプレートリポジトリを別に建てる案もあるが、
共通部の変更を各製品へ配る仕組みが別途必要になる。製品が数個のうちは
「実物を雛形にする」ほうが安い。

## 複製後に差し替える場所

### リポジトリ設定（コードの外）
- [ ] **Actions のリポジトリ変数 `PACKAGE_NAME`** を新パッケージIDに
      （Settings → Secrets and variables → Actions → Variables。
      `build-listing.yml` が参照する）
- [ ] **Pages を有効化**（Settings → Pages → Source: GitHub Actions）

### パッケージ本体
- [ ] `Packages/com.tsukumodo.avatar-omamori/` → 新パッケージIDにリネーム
- [ ] その `package.json`（name / displayName / description / version / vpmDependencies）
- [ ] asmdef・名前空間・README

### Website/（リスティングページ）
- [ ] `index.html`: 製品固有の全文（口上・守り札・動作環境・バッジ・OGP・落款の説明）。
      Scriban プレースホルダと構造は残す
- [ ] `styles.css`: **商品色トークンだけ**を新商品の色に差し替える
      （`--omamori-green` 系）。店の紺・金茶・暖簾・落款は「つくも堂」の共通装いなので
      変えない（詳細は `.claude/skills/tsukumodo-design/`）
- [ ] `omamori-thumbnail.png` → 新商品のサムネイル（OGP画像を兼ねる）
- [ ] `favicon.ico`（使い回すなら不要）
- [ ] OGP の絶対URL（`og:image` / `twitter:image`）を新リポの Pages URL に

### プレビュー機構
- [ ] `tools/listing-preview/fixtures/listing.json` を新パッケージの
      **本番 package.json と同じ形**に（空の任意項目は空のまま）

### 守るべき前提（全製品共通）
- 各製品のページは**その製品ひとつだけ**を載せる（BOOTH → 同梱PDF → ページ → VCC の一本道）
- GitHubリンク・zip直リンクを出さない、ALCOMは名前のみ、報告先は X の DM
- 公開前に `.claude/skills/listing-check/` のチェックリストを全部通す
