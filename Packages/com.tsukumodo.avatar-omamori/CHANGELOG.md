# Changelog

## [Unreleased]
### 追加
- 自動修正の基盤を追加（CheckResult に FixAction を持たせ、UI に「修正」ボタンを表示）
- #8 Animator Layer Weight=0 の自動修正を実装（Weight=0 を 1 に変更、Undo 対応）

## [0.4.0] - 2026-04-11
### Added
- [SDK] Animator Layer Weight チェック（FXレイヤーの Weight=0 を検出・警告）
- [SDK] 同期パラメータ上限チェック（同期パラメータの合計ビット数が256bitを超える場合をエラー）

## [0.3.1] - 2026-04-03

### Changed
- [MA] MAObjectToggleSelfRefCheck を MAObjectToggleCheck に改名（ObjectToggle全般のバリデーションとして整理）

## [0.3.0] - 2026-04-03

### Added
- [MA] 装飾物の未セットアップ検出チェック（Armatureを持つ子オブジェクトにMA Merge Armature / Bone Proxyが未設定の場合を警告。ターゲット未設定も検出）
- [SDK] Expression Parameter 空欄チェック（名前が空のParameterエントリを警告）
- [SDK] Expression Menu設定済み・Parameters未設定の検出
- [MA] ObjectToggle 空ターゲット検出（トグルリストのターゲットが空の場合を警告）

## [0.2.0] - 2026-03-28

### Added
- [Unity] Missing Script 検出チェック（アバター配下のMissing Scriptを検出・致命的エラー）
- [Shader] シェーダー未検出（ピンクマテリアル）チェック（シェーダーが見つからないマテリアルを検出・警告）

## [0.1.0] - 2026-03-27

### Added
- VRC Avatar Descriptor 重複チェック
- MA MenuItem 未接続チェック（祖先に MenuInstaller がない MenuItem を検出）
- MA ObjectToggle 自己参照チェック（自身の GameObject をトグル対象にしているケースを検出）
- EditorWindow UI（Tools > アバター改変おまもり）
- Severity 別（Error / Warning / Info）のグループ表示
- オブジェクト選択（Ping + Selection）機能
