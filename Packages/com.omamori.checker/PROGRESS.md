# PROGRESS - アバター改変おまもりツール

最終更新: 2026-03-27

## 現在のステータス

🟢 Phase 1 MVP 実装完了・動作確認済み

## ステップ一覧

- [x] ステップ1: Phase 1 MVP 実装（VPMパッケージ構造 + 3つのチェック機能 + EditorWindow UI）
- [ ] ステップ2: Phase 2 チェック追加・機能拡張（未着手）
- [ ] ステップ3: テスト拡充・品質向上（未着手）
- [ ] ステップ4: BOOTH販売準備
- [ ] ステップ5: 公開・告知

## 完了したこと

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

## 今やっていること

- 特になし（Phase 1 完了、次のステップ待ち）

## BOOTH販売に向けて解禁された要素

- VPMパッケージとしてインストール可能な形式が完成
- 基本的な3チェックが動作確認済み
- MIT License 設定済み

## 未解決の課題

- Phase 2 以降のチェック項目の選定（どのチェックを追加するか）
- VPMリポジトリ（repos.json）の整備（VCC経由でのインストール対応）
- BOOTH向けの説明文・スクリーンショット・サムネイル準備
- 実際の複数アバターでの網羅的テスト

## 関連リンク

- [[README]]
- [[販売計画]]
- [[作業ログ]]
