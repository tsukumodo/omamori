# アバター改変おまもり

VRChatアバターの設定ミスをビルド前に一括検出するUnity Editor拡張です。

## 機能

| チェック | 内容 | 深刻度 |
|---|---|---|
| VRC Avatar Descriptor 重複チェック | アバタールート以下にDescriptorが複数あるか検出 | Error |
| MA MenuItem 未接続チェック | MenuItemの祖先にMenuInstallerがない場合を検出 | Error |
| MA ObjectToggle 自己参照チェック | ObjectToggleが自身のGameObjectを参照している場合を検出 | Error |
| Missing Script チェック | アバター配下のMissing状態のコンポーネントを検出 | Error |
| シェーダー未検出チェック | シェーダーが見つからないマテリアル（ピンク表示）を検出 | Warning |
| MA 装飾物の未セットアップチェック | Armatureを持つ子オブジェクトにMerge Armature / Bone Proxyが未設定の場合を検出 | Warning |
| Expression Parameter 空欄チェック | Expression Parametersに名前が空のエントリがある場合を検出 | Warning |

## 動作要件

- Unity 2022.3 以降
- VRChat SDK - Avatars 3.5.0 以降
- Modular Avatar（オプション — 未インストール時はMAチェックをスキップ）

## ダウンロード

[BOOTH](https://tsukumodo-lab.booth.pm/items/8132860) からダウンロードしてください。

## 使い方

1. Unity メニューから **Tools > アバター改変おまもり** を開く
2. アバタールートの GameObject を指定
3. 「チェック実行」ボタンをクリック
4. 結果が Severity 別（Error / Warning / Info）に表示される
5. 「選択」ボタンで問題のあるオブジェクトにフォーカス

## 利用統計について（v0.6.0〜）

今後の改善のために、おまもりは**ごく簡単な利用統計をあなたのPC内（プロジェクトの `Library/` フォルダ）にのみ記録**します。データが自動で送信されることは一切ありません。

### 記録する項目
- チェック種別ごとの検出件数（例: Missing Script チェックが何件検出したか）
- 修正種別ごとの自動修正の実行回数
- チェックの実行回数
- 日付（年月日のみ）
- おまもりのバージョン

### 記録しない項目
- アバター名・アバターのGUID
- シーン名・ファイルパス
- PC名・ユーザー名
- 時刻（時分秒）

### 確認・管理する
- **Tools > つくも堂 > 使用統計を見る** から、記録内容の確認・「フィードバックとしてコピー」・統計のクリア・収集の無効化／再開ができます。
- もしよければ、コピーした内容を つくも堂の X（[@tsukumodo_lab](https://x.com/tsukumodo_lab)）の DM で送っていただけると、今後のチェック項目の検討に役立ちます（任意です）。

保存先: `<プロジェクト>/Library/com.tsukumodo.avatar-omamori/usage-stats.json`

## ライセンス

MIT License — 詳しくは [LICENSE.md](LICENSE.md) をご覧ください。
