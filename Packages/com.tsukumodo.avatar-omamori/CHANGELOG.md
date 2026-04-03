# Changelog

## [0.3.0] - 2026-04-03

### Added
- [MA] 装飾物の未セットアップ検出チェック（Armatureを持つ子オブジェクトにMA Merge Armature / Bone Proxyが未設定の場合を警告）
- [SDK] Expression Parameter 空欄チェック（名前が空のParameterエントリを警告）

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
