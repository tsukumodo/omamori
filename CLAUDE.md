# CLAUDE.md — アバター改変チェックツール「おまもり」

## プロジェクト概要

VRChatアバターの改変設定ミスをビルド前に検出するUnity Editor拡張ツール。
BOOTHで配布予定。VPMパッケージとして提供する。

- **ターゲットユーザー**: VRChatアバター改変初心者〜中級者
- **解決する課題**: MA設定ミス、SDK設定不備などによるアップロード失敗や意図しない挙動

## 技術スタック

- **言語**: C#
- **Unity**: 2022.3.x（VRChat推奨バージョン）
- **必須依存**: VRChat SDK（Avatars）
- **任意依存**: Modular Avatar（MA）— **直接参照禁止、Reflection経由でアクセス**
- **配布形式**: VPMパッケージ
- **パッケージ名**: `com.tsukumodo.avatar-omamori`（旧: `com.omamori.checker` — 使用禁止）

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

## ディレクトリ構成

### パッケージ本体

```
Packages/com.tsukumodo.avatar-omamori/
├── package.json
├── Editor/
│   ├── com.tsukumodo.avatar-omamori.editor.asmdef
│   ├── AvatarOmamoriWindow.cs            # メインEditorWindow
│   ├── CheckResult.cs                     # チェック結果のデータクラス
│   ├── CheckRunner.cs                     # チェック実行ランナー
│   ├── IAvatarCheck.cs                    # チェック項目の共通インターフェース
│   ├── Checks/
│   │   ├── DescriptorDuplicateCheck.cs    # VRC Descriptor重複検出
│   │   ├── MAMenuItemUnboundCheck.cs      # MA Menu Item未接続検出
│   │   └── MAObjectToggleSelfRefCheck.cs  # Object Toggle自己参照検出
│   └── Util/
│       └── MAReflectionHelper.cs          # MA関連のReflectionアクセスを集約
├── LICENSE.md
├── README.md
└── CHANGELOG.md
```

### GitHub リポジトリ全体構成（VPM template-package ベース）

```
omamori/                               # https://github.com/tsukumodo/omamori
├── .github/workflows/
│   ├── release.yml                    # Build Release（手動実行でリリース作成）
│   └── build-listing.yml             # Build Repo Listing（GitHub Pages更新）
├── Packages/
│   └── com.tsukumodo.avatar-omamori/  # パッケージ本体（上記）
├── Website/                           # VPMランディングページ
│   ├── index.html
│   ├── styles.css
│   ├── app.js
│   ├── omamori-thumbnail.png
│   ├── tsukumodo-header.png
│   └── tsukumodo-logo.png
├── Resources/                         # ブランド画像素材
│   ├── omamori-thumbnail-1440.png
│   ├── omamori-thumbnail-2160.png
│   ├── tsukumodo-header-center-1500x500.png
│   ├── tsukumodo-logo-256.png
│   └── tsukumodo-logo-512.png
├── Assets/
├── ProjectSettings/
└── README.md
```

### ローカル作業ディレクトリ

- **GitHubリポジトリ（メイン）**: `C:\Users\ryota-mochizuki\omamori-repo`
- **旧開発フォルダ（バックアップ）**: `C:\Users\ryota-mochizuki\MyFiles\03_趣味_Hobby\ゲーム\VRChat\ツール\avatar-kaihen-omamori`
- ※ 今後の開発は `omamori-repo` のみで行う。旧フォルダは参照不要

## コーディング規約

- **パッケージ名**: `com.tsukumodo.avatar-omamori`（旧: `com.omamori.checker` — 使用禁止）
- **名前空間**: `Omamori.Editor`
- **コメント**: 日本語で記述
- **エラーメッセージ**: 日本語で記述
- **nullチェック**: 徹底すること（Unityの `== null` は `UnityEngine.Object` の特殊挙動があるため注意）
- **MAのReflectionアクセス**: 必ず `MAReflectionHelper` クラスに集約し、他のクラスから直接Reflectionを呼ばない

## 重要な実装ルール

### MA（Modular Avatar）関連

1. **MAのクラス名は推測で実装しないこと** — 必ず本ファイルの「MA実装リファレンス」セクションを参照する。記載がない場合はGitHubリポジトリ（https://github.com/bdunderscore/modular-avatar）で確認し、結果を「MA実装リファレンス」に追記すること
1. **asmdefにMAへの直接参照を入れない** — MA未インストール環境でコンパイルエラーになるため
1. **MA未インストール時の挙動**: MA関連チェックをスキップし、「Modular Avatarが検出されませんでした。MA関連のチェックはスキップされます」と表示する

### Unity固有の注意点

- `GetComponentsInChildren<T>(true)` の `true` を忘れない（非アクティブなGameObjectも走査するため）
- `Selection.activeGameObject` を変更する場合は `EditorGUIUtility.PingObject` も併用してHierarchyで点滅ハイライトさせる
- `package.json` と `asmdef` の整合性を確認する

### VPMリリース手順

1. `package.json` の `version` を更新
2. `git commit` & `git push origin main`
3. GitHub Actions → 「Build Release」を手動実行
4. リリース成功後、「Build Repo Listing」が自動実行されてVPM LPが更新される
5. 自動実行されない場合は「Build Repo Listing」を手動実行

### Git設定の注意

- `omamori-repo` のgit configはローカルのみ（`--global`なし）でつくも堂の情報を設定済み
- 仕事用のglobal設定と干渉しないようにするため、`--global` は使わないこと

## MA実装リファレンス

> **このセクションはソースコード調査で確認済みの事実のみを記載する。**
> 新たなMAクラスにアクセスする必要が生じた場合、GitHubリポジトリで確認し、ここに追記すること。

### パッケージ情報

|項目       |値                          |
|---------|---------------------------|
|パッケージ名   |`nadena.dev.modular-avatar`|
|表示名      |Modular Avatar             |
|依存フレームワーク|NDMF (`nadena.dev.ndmf`)   |

### Reflectionで使用するクラス一覧

|用途         |完全修飾クラス名                                                   |アセンブリ                                |確認元                             |
|-----------|-----------------------------------------------------------|-------------------------------------|--------------------------------|
|メニュー項目     |`nadena.dev.modular_avatar.core.ModularAvatarMenuItem`     |`nadena.dev.modular-avatar` (Runtime)|Runtime/ModularAvatarMenuItem.cs|
|メニューインストーラー|`nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller`|`nadena.dev.modular-avatar` (Runtime)|GitHub Issue #68 での言及           |
|オブジェクトトグル  |`nadena.dev.modular_avatar.core.ModularAvatarObjectToggle` |`nadena.dev.modular-avatar` (Runtime)|公式ドキュメント Object Toggle ページ      |

### 注意事項

- Runtimeコンポーネントの名前空間は `nadena.dev.modular_avatar.core`（ドットではなくアンダースコア）
- Editorクラスの名前空間は `nadena.dev.modular_avatar.core.editor`
- コンポーネントは `AvatarTagComponent` を継承している

## チェック項目（Phase 1）

### 1. VRC Avatar Descriptor 重複検出

- アバターのルートと子オブジェクト両方にDescriptorがある場合を検出
- **深刻度**: 致命的エラー（🔴）

### 2. MA Menu Item 未接続（Unbound）検出

- MA Menu Itemが存在するのにMA Menu Installerが祖先に無い場合を検出
- **深刻度**: 致命的エラー（🔴）
- **前提**: MAがインストールされている場合のみ実行

### 3. Object Toggle 自己参照検出

- Object Toggleのターゲットが自分自身を指している場合を検出
- **深刻度**: 警告（🟡）
- **前提**: MAがインストールされている場合のみ実行

## UI仕様

### EditorWindow構成

1. **アバター選択フィールド**: `ObjectField`でシーン上のアバターを指定
1. **チェック実行ボタン**: クリックで全チェックを実行
1. **結果表示エリア**: 3段階で表示

- 🔴 **致命的エラー**: 赤背景。ビルド失敗または重要機能が動作しない
- 🟡 **警告**: 黄背景。動作はするが意図しない挙動の可能性
- ℹ️ **情報**: 灰背景。改善提案

### 結果表示の各項目に含める情報

- カテゴリタグ（`[SDK]`, `[MA]` 等）
- 問題の説明（日本語）
- 該当オブジェクト名（クリックでHierarchyで選択＆ハイライト）
- 修正方法のヒント

### UXルール

- チェック実行前: 「アバターを指定して『チェック実行』を押してください」と表示
- 結果0件: 「問題は見つかりませんでした ✅」と表示
- 結果はFoldout（折りたたみ）でカテゴリ別に表示

## テストシナリオ

実装後、以下で動作確認すること:

1. **正常なアバター** → チェック結果0件
1. **Descriptor重複** → エラー検出
1. **MA未インストール環境** → コンパイルが通り、SDK関連チェックだけ動作
1. **MA Menu Item単独配置**（Menu Installerなし）→ Unboundエラー検出
1. **Object Toggle自己参照** → 警告検出

## Phase 1 完了条件

- [x] 上記3チェック項目が動作するEditorWindowが完成
- [x] VPMパッケージ構造になっている
- [x] MA未インストールでもコンパイルが通る
- [x] 日本語のエラーメッセージが表示される
- [x] 該当オブジェクトをクリックでHierarchy上で選択できる
- [x] GitHubリポジトリ公開・VPMリリース v0.1.0 作成
- [x] VCCからのインストール動作確認

## 今後の拡張予定（Phase 2以降 — Phase 1では実装しない）

Phase 1ではコード構造を拡張しやすく設計しておくこと（`IAvatarCheck`インターフェースで新チェック項目を追加しやすくする）。

- Missing Script検出
- シェーダー未検出（ピンクマテリアル）検出
- 同期パラメータ256bit超過チェック
- Parameter欄が空のチェック
- ボーン名不一致チェック
- AAO未設定チェック
- テクスチャVRAM計算
- ワンクリック自動修正機能

-----

## CLAUDE.md 自己改善ルール

> **このCLAUDE.md自体を「生きたドキュメント」として維持する。**
> Claude Codeは実装作業中に以下のルールに従い、CLAUDE.mdの改善を提案・実行する。

### 基本方針

- CLAUDE.mdはプロジェクトの信頼できる唯一の情報源（Single Source of Truth）とする
- コードの実装とCLAUDE.mdの記載に矛盾が生じた場合、CLAUDE.mdを更新して整合性を保つ
- 推測や未検証の情報は記載しない。記載する場合は `[未検証]` タグを付与する

### 更新トリガーと対応アクション

|トリガー                 |アクション                  |承認       |
|---------------------|-----------------------|---------|
|MAの新しいクラス名・プロパティを調査した|「MA実装リファレンス」に追記        |不要（事実の記録）|
|チェック項目を追加・変更・削除した    |「チェック項目」セクションを更新       |**要承認**  |
|実装中にハマった問題と解決策を見つけた  |「実装ノート（トラブルシューティング）」に追記|不要（知見の記録）|
|ディレクトリ構成やファイルを追加・変更した|「ディレクトリ構成」を更新          |不要（事実の記録）|
|UI仕様を変更した            |「UI仕様」セクションを更新         |**要承認**  |
|Phase計画に変更が生じた       |該当Phaseセクションを更新        |**要承認**  |
|コーディング規約の追加・変更が必要    |「コーディング規約」に追記          |**要承認**  |
|テストシナリオの追加・変更が必要     |「テストシナリオ」を更新           |**要承認**  |

### 承認フロー

「**要承認**」の項目については、Claude Codeは以下のフォーマットで提案すること:

```
📝 CLAUDE.md 更新提案
━━━━━━━━━━━━━━━━━━━━
対象セクション: （セクション名）
種別: 追加 / 変更 / 削除
理由: （なぜこの更新が必要か）

【変更内容】
（差分または新しい内容をmarkdownで記載）
━━━━━━━━━━━━━━━━━━━━
この変更をCLAUDE.mdに反映しますか？
```

人間が承認した場合のみCLAUDE.mdに反映する。却下された場合はその理由を確認し、必要に応じて再提案する。

### 「不要（事実の記録）」の更新ルール

承認不要の更新であっても、以下を守ること:

- 更新した旨をチャットで報告する（例:「CLAUDE.mdの『MA実装リファレンス』にXxxClassの情報を追記しました」）
- 既存の記載と矛盾する場合は、上書きではなく「要承認」に格上げする
- 大量の変更（5箇所以上の同時変更）になる場合も「要承認」に格上げする

### 禁止事項

- 推測に基づくクラス名・名前空間の記載（必ずソースで確認してから記載）
- 「要承認」項目を承認なしに反映すること
- CLAUDE.mdの自己改善ルール自体を無断で変更すること（このセクションの変更は常に「要承認」）

## 実装ノート（トラブルシューティング）

> **実装中に遭遇した問題と解決策を蓄積するセクション。**
> Claude Codeは問題を解決したら、ここに簡潔に記録すること。

<!--
記載フォーマット:
### タイトル（日付）
- **問題**: 何が起きたか
- **原因**: なぜ起きたか
- **解決策**: どう解決したか
- **教訓**: 今後注意すべきこと
-->

### PowerShellでのUTF-8 BOM問題（2026-03-27）
- **問題**: package.jsonをPowerShellで書き換えた後、GitHub ActionsのJSONパーサーがエラー（`Unexpected token '﻿'`）
- **原因**: `[System.IO.File]::WriteAllText()` でデフォルトの `System.Text.Encoding.UTF8` を使うとBOM付きになる
- **解決策**: `New-Object System.Text.UTF8Encoding($false)` でBOMなしエンコーディングを使用
- **教訓**: GitHub ActionsのJSONパーサーはBOMを受け付けない。package.jsonは必ずBOMなしUTF-8で保存すること

### パッケージ名・フォルダ名の不一致（2026-03-27）
- **問題**: フォルダ名 `com.omamori.checker` とpackage.jsonの `name: com.tsukumodo.avatar-omamori` が不一致で、GitHub Actionsがパッケージを見つけられない
- **原因**: 開発初期にフォルダ名を仮で決めたまま、package.jsonだけ正式名に更新していた
- **解決策**: `git mv` でフォルダ名を `com.tsukumodo.avatar-omamori` に変更。同時に `Packages/.gitignore` の許可リストとGitHub変数 `PACKAGE_NAME` も更新
- **教訓**: VPMテンプレートでは「フォルダ名 = package.json の name = GitHub変数 PACKAGE_NAME」が全て一致している必要がある

### Packages/.gitignore によるファイル除外（2026-03-27）
- **問題**: `git add` してもパッケージフォルダが追加されない
- **原因**: テンプレートの `Packages/.gitignore` が `/*/` で全フォルダを除外し、許可リスト（`!com.vrchat.demo-template`）に新パッケージ名が入っていなかった
- **解決策**: `Packages/.gitignore` の許可リストを `!com.tsukumodo.avatar-omamori` に更新
- **教訓**: VPMテンプレートを使う場合、`Packages/.gitignore` の許可リスト更新を忘れないこと

### GitHub Organization で仕事用と分離（2026-03-27）
- **問題**: 仕事用GitHubアカウントで公開リポジトリを作りたくない
- **解決策**: 無料のOrganization `tsukumodo` を作成し、その下にリポジトリを配置。メンバー公開設定をPrivateにすれば個人アカウント名は外に出ない
- **教訓**: リポジトリのgit configもローカルのみ（`--global`なし）でつくも堂の情報を設定し、仕事用のglobal設定と干渉しないようにする
