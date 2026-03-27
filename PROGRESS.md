# PROGRESS - アバター改変おまもりツール

最終更新: 2026-03-27

## 現在のステータス

🟢 Phase 1 MVP 実装完了 → VPMリリース v0.1.0 公開済み

## ステップ一覧

- [x] ステップ1: Phase 1 MVP 実装（VPMパッケージ構造 + 3つのチェック機能 + EditorWindow UI）
- [x] ステップ2: GitHub + VPMリポジトリ整備・初回リリース
- [ ] ステップ3: BOOTH販売準備（説明文・スクリーンショット・サムネイル）
- [ ] ステップ4: BOOTH公開（無料版 + 開発支援版）
- [ ] ステップ5: Phase 2 チェック追加・機能拡張
- [ ] ステップ6: テスト拡充・品質向上

## 完了したこと

### Phase 1 MVP（以前のセッション）
- VPMパッケージ骨格作成（package.json, asmdef, .gitignore, LICENSE.md）
- コアインフラ実装（CheckResult, IAvatarCheck, CheckRunner）
- MAReflectionHelper（Modular Avatar をReflectionで動的参照、未インストール時はスキップ）
- チェック機能 3種実装
  - VRC Avatar Descriptor 重複チェック（Error）
  - MA MenuItem 未接続チェック（Error）— 祖先にMenuInstallerがない場合を検出
  - MA ObjectToggle 自己参照チェック（Error）— MA-1200 ビルドエラーの原因を検出
- EditorWindow UI（Tools > アバター改変おまもり）
  - Severity別Foldout表示（Error / Warning / Info）
  - カード+アイコン風の見やすいレイアウト
  - オブジェクト選択（Ping + Selection）ボタン
- 各エラーメッセージに「何が起こるか」の影響説明を追記
- README.md / CHANGELOG.md 作成
- Unityプロジェクト（Komano_Sukajan）にシンボリックリンクで導入し動作確認済み

### GitHub + VPMリポジトリ整備（2026-03-27）
- GitHub Organization `tsukumodo` を作成（仕事用アカウントと分離）
- VRChat公式 `template-package` ベースでリポジトリ作成: https://github.com/tsukumodo/omamori
- GitHub Pages 設定（Source = GitHub Actions）
- パッケージ名を `com.tsukumodo.avatar-omamori` に統一（フォルダ名・package.json・GitHub変数）
- package.json の文字化け修正・BOM除去
- Packages/.gitignore の許可リスト修正
- GitHub Actions「Build Release」で v0.1.0 リリース作成成功
- VCCからのインストール動作確認済み（URL: https://tsukumodo.github.io/omamori/）

### VPMランディングページ（2026-03-27）
- つくも堂ブランドに合わせたLPリデザイン
  - 上部: つくも堂ヘッダー画像（紺背景 #162244）
  - ヒーロー: おまもりサムネイル + タイトル + バッジ（抹茶グリーン #467A56）
  - 中央: VCC追加ボタン + パッケージ一覧のみ（機能カードなし）
  - フッター: つくも堂ロゴ 48px + テキスト
  - フォント: Zen Maru Gothic + Noto Sans JP
  - ダークモード対応・レスポンシブ対応

## 今やっていること

- 特になし（次のステップ = BOOTH販売準備）

## 次にやること（BOOTH販売準備）

- BOOTH商品ページの説明文仕上げ（以前作ったドラフトベース）
- EditorWindowのスクリーンショット撮影
- 商品サムネイル作成（抹茶トーン）
- 無料版 + 開発支援版（¥500〜¥800）の出品

## 未解決の課題

- Phase 2 以降のチェック項目の選定
- 実際の複数アバターでの網羅的テスト
- git config (user.email, user.name) は omamori-repo ローカルのみ設定済み（global未設定）

## リポジトリ・配布情報

| 項目 | 値 |
|------|-----|
| GitHub Organization | tsukumodo |
| リポジトリ | https://github.com/tsukumodo/omamori |
| パッケージ名 | com.tsukumodo.avatar-omamori |
| VPM LP | https://tsukumodo.github.io/omamori/ |
| 現在のバージョン | 0.1.0 |
| ライセンス | MIT |
| BOOTH | tsukumodo-lab.booth.pm（未出品） |

## 関連リンク

- [[README]]
- [[販売計画]]
- [[作業ログ]]