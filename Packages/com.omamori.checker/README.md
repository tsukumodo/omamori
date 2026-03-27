# アバター改変おまもり

VRChatアバターの設定ミスをビルド前に一括検出するUnity Editor拡張です。

## 機能

| チェック | 内容 | 深刻度 |
|---|---|---|
| VRC Avatar Descriptor 重複チェック | アバタールート以下にDescriptorが複数あるか検出 | Error |
| MA MenuItem 未接続チェック | MenuItemの祖先にMenuInstallerがない場合を検出 | Error |
| MA ObjectToggle 自己参照チェック | ObjectToggleが自身のGameObjectを参照している場合を検出 | Error |

## 動作要件

- Unity 2022.3 以降
- VRChat SDK - Avatars 3.5.0 以降
- Modular Avatar（オプション — 未インストール時はMAチェックをスキップ）

## インストール

### VPM (推奨)

VCC (VRChat Creator Companion) に本リポジトリを追加し、パッケージをインストールしてください。

### 手動

1. `Packages/` フォルダに本リポジトリをクローンまたはコピー
2. Unity がコンパイルし、`Tools > アバター改変おまもり` メニューが表示されることを確認

## 使い方

1. Unity メニューから **Tools > アバター改変おまもり** を開く
2. アバタールートの GameObject を指定
3. 「チェック実行」ボタンをクリック
4. 結果が Severity 別（Error / Warning / Info）に表示される
5. 「選択」ボタンで問題のあるオブジェクトにフォーカス

## ライセンス

MIT License — 詳しくは [LICENSE.md](LICENSE.md) をご覧ください。
