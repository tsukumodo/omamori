# アバター改変おまもり

VRChatアバターの設定ミスをビルド前に一括検出するUnity Editor拡張です。

## 機能

| チェック | 内容 | 深刻度 |
|---|---|---|
| VRC Avatar Descriptor 重複チェック | アバタールート以下にDescriptorが複数あるか検出 | Error |
| MA MenuItem 未接続チェック | MenuItemの祖先にMenuInstallerがない場合を検出 | Error |
| MA ObjectToggle チェック | ターゲットが空（Warning）、または自身のGameObjectを参照している自己参照（Error）を検出 | Warning / Error |
| Missing Script チェック | アバター配下のMissing状態のコンポーネントを検出 | Error |
| シェーダー未検出チェック | シェーダーが見つからないマテリアル（ピンク表示）を検出 | Warning |
| MA 装飾物の未セットアップチェック | Armatureを持つ子オブジェクトにMerge Armature / Bone Proxyが未設定の場合を検出 | Warning |
| Expression Parameter チェック | Expression Menuが設定されているのにExpression Parametersが未設定、またはパラメータ名が空のエントリがある場合を検出 | Warning |
| Animator Layer Weight チェック | FXレイヤーのWeightが0になっている場合を検出。アニメーションが反映されない原因になる | Warning |
| 同期パラメータ上限チェック | Expression Parametersの同期パラメータ合計ビット数が256bitを超えている場合を検出。超過するとアップロードに失敗する | Error |
| Emission（意図しない発光）チェック | マテリアルのEmission（発光）が意図せず有効になっている場合を検出。対象はlilToonとUnity Standard系のシェーダー | Warning |

## パフォーマンス表示

チェック結果とは別に、アバターのパフォーマンスも確認できます。ランクは Error / Warning / Info のような深刻度を持たない参考情報のため、上の「機能」の表とは別枠で表示されます。

- PC と Quest / iOS の両方について、総合ランクと「ランクを下げている項目」を表示
- 各項目に「あと何をどこまで減らすと1ランク上がるか」を表示。目標値は VRChat SDK が持つ閾値をそのまま使うため、SDK の更新にも追従する
- Quest / iOS で使えない要素（非対応シェーダー・Quest では無効になるコンポーネント・Unity の Constraint）を件数と影響つきで表示。対象は Hierarchy で選択できる
- 各項目の「調べる」ボタンから、項目に応じた VRChat 公式ドキュメントを開ける

⚠ ここに表示される数値は、Modular Avatar などの非破壊改変ツールを適用する前のものです。最終的な値は VRChat SDK のビルドパネルでご確認ください。

## 動作要件

- Unity 2022.3 以降
- VRChat SDK - Avatars 3.7.0 以降（パフォーマンス表示の Constraint 項目が 3.7.0 で追加された値を使うため）
- Modular Avatar（オプション — 未インストール時はMAチェックをスキップ）

## ダウンロード

[BOOTH](https://tsukumodo-lab.booth.pm/items/8132860) からダウンロードしてください。

## 使い方

1. Unity メニューから **Tools > アバター改変おまもり** を開く
2. アバタールートの GameObject を指定
3. 「チェック実行」ボタンをクリック
4. 結果が Severity 別（Error / Warning / Info）に表示される
5. 「選択」ボタンで問題のあるオブジェクトにフォーカス
6. 項目によっては「修正」ボタンで自動修正できる。多くは Undo（Ctrl+Z）で戻せるが、一部戻せないものもあり、実行前の確認ダイアログで案内される
7. 「カードを保存」ボタンで、結果をSNS共有向けのカード画像（PNG）として書き出せる

## 利用統計について（v0.6.0〜）

今後の改善のために、おまもりは**ごく簡単な利用統計をあなたのPC内（プロジェクトの `Library/` フォルダ）にのみ記録**します。データが自動で送信されることは一切ありません。

### 記録する項目
- チェック種別ごとの検出件数（例: Missing Script チェックが何件検出したか）
- 修正種別ごとの自動修正の実行回数
- チェックの実行回数
- 日付（年月日のみ）
- おまもりのバージョン
- フィードバックとしてコピーした日（年月日のみ。まだコピーしていなければ記録されません）
- 利用統計の収集を無効にしているかどうか
- 記録データの形式のバージョン

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
