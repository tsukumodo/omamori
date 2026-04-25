# Changelog

## [Unreleased]
### 追加
- アバタールート指定時にチェックを自動実行するように変更（従来は「チェック実行」ボタン押下が必須。ボタンは引き続き手動再チェック用に利用可能）
- 自動修正の基盤を追加（CheckResult に FixAction を持たせ、UI に「修正」ボタンを表示）
- #8 Animator Layer Weight=0 の自動修正を実装（Weight=0 を 1 に変更、Undo 対応）
- #4 Missing Script の自動修正を実装（GameObject 上の Missing Script を一括削除。Unity の API 仕様で Undo 不可だが、Missing Script は元々参照が壊れているため実質的なデータ損失はなく、事前確認でその旨を明示）
- #1 VRC Avatar Descriptor 重複の自動修正を実装（複数の Descriptor から「どれを残すか」を選ぶドロップダウンで解消。本体が推奨、選んだもの以外は一括削除で Undo 対応）
- 共通基盤に SkipConfirm フラグを追加（修正処理が独自の UI（ダイアログ・ドロップダウン等）を出す場合に、共通基盤側の事前確認と自動再チェックをスキップする）

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
