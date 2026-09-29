using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// 服・装飾品の「まとまり」別パフォーマンス内訳を表示する EditorWindow
    /// （v0.11.0 W0 設計 §11 / DEC-109）。
    ///
    /// <para>
    /// 主画面には「どの服・パーツが重いか見る」ボタン1行だけを足し、内訳はこのウィンドウに出す。
    /// 主画面へインライン展開すると縦幅が 200〜250px 増え、DEC-097 が示した
    /// 「再燃したら情報量を削る」方向と逆行するため（制約 C3）。
    /// </para>
    /// <para>
    /// プラットフォーム（PC / Quest）では分けない。<c>AnalyzeMaterials</c> はビルドターゲットを
    /// 一切参照しないと IL 解析で確定しており、内訳は PC / Quest で必ず同一になる（W0 設計 §2.5）。
    /// 「Quest の内訳」と名乗らせないことも含めて、プラットフォーム名はこのウィンドウに出さない。
    /// </para>
    /// <para>
    /// 表示は差分方式（「このまとまり／パーツを外すと ○MB 減ります」）。単独帰属にすると共有テクスチャが
    /// 多重計上され、合計が実際の数倍に水増しされる（8/8 実測: 850.3MB vs 実際 336.8MB）。
    /// まとまりの中で絵柄を使い回している服はパーツ単位だと全パーツ「共有」になり、一番重い服が
    /// 一番軽く見える（T-8 実機確認）。まとまりごと外した差分を別に持つことでそこに答える（DEC-109）。
    /// </para>
    /// <para>
    /// まとまりの決め方（後から足したプレハブ／入れ物の一番外側を1まとまりにする等）は
    /// <see cref="AvatarPartGrouping"/> 側の責務。このクラスは <see cref="PerformanceBreakdown.Groups"/>
    /// の並びと中身をそのまま描画するだけで、まとまりの判定ロジックは持たない。
    /// </para>
    /// </summary>
    internal sealed class PerformanceBreakdownWindow : EditorWindow
    {
        private const string WindowTitle = "おまもり — パフォーマンスの内訳";
        private const float SelectButtonWidth = 48f;
        private const float TextureColumnWidth = 90f;
        private const float PolyColumnWidth = 70f;

        // 対象は UnityEngine.Object 参照なのでドメインリロードをまたいで復元されるが、
        // スナップショットは [NonSerialized] で捨てる。寿命を分けているのは意図的で、
        // 「リロード後は対象を覚えたまま、中身だけ [再計算] を促す」状態にするため。
        // 復元済みかどうかの判定は null ではなく件数（Groups.Count）で見る（CLAUDE.md 実装ノートの教訓）。
        [SerializeField] private GameObject _target;

        [NonSerialized] private PerformanceBreakdown _breakdown;
        [NonSerialized] private string _failureMessage;

        // まとまりの開閉状態。まとまり名（アバター本体は AvatarPartGrouping.AvatarBodyName という固定キー）
        // をキーにした「閉じている」集合として持つ。既定は空＝全部開く（W0 設計 §11.2 / DEC-073 の C1）。
        // ドメインリロードをまたいで保つ必要はない（_breakdown 同様、開いたときは既定に戻ってよい）が、
        // 同一セッション内での [再計算] では保つ（この集合には触らない）。
        [NonSerialized] private HashSet<string> _collapsedGroups;

        [NonSerialized] private GUIStyle _boldFoldoutStyle;

        private Vector2 _scroll;

        /// <summary>
        /// 内訳ウィンドウを開き、<paramref name="avatarRoot"/> の内訳を計算して表示する。
        /// ドッキング可・シングルトン（<c>UsageStatsWindow</c> と同じ形）。
        /// </summary>
        internal static void Open(GameObject avatarRoot)
        {
            var window = GetWindow<PerformanceBreakdownWindow>(WindowTitle);
            window.minSize = new Vector2(560f, 460f);
            window._target = avatarRoot;
            window.Recalculate();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(4);

            DrawHeader();

            // データが無いところから始める（ドメインリロード後はここに来る）
            if (_breakdown == null || _breakdown.Groups.Count == 0)
            {
                DrawEmptyState();
                EditorGUILayout.Space(8);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(4);
            DrawTotalsLine();

            EditorGUILayout.Space(8);
            DrawTable();

            EditorGUILayout.Space(10);
            DrawNotes();

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var mainWindowTarget = FindMainWindowTarget();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                _target != null ? $"対象: {_target.name}" : "対象: —", EditorStyles.boldLabel);
            if (GUILayout.Button("再計算", EditorStyles.miniButton, GUILayout.Width(72)))
            {
                // 押されたときだけ主画面の対象を引き取る。主画面から自動で押し込む結合は作らない
                if (mainWindowTarget != null) _target = mainWindowTarget;
                Recalculate();
            }
            EditorGUILayout.EndHorizontal();

            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    "対象のアバターが見つかりません。シーンから外れたか、削除された可能性があります。",
                    MessageType.Info);
                return;
            }

            if (mainWindowTarget != null && mainWindowTarget != _target)
            {
                EditorGUILayout.HelpBox(
                    $"おまもり本体の対象が変わりました。ここに出ているのは「{_target.name}」を開いたときの内訳です。"
                    + "[再計算] を押すと今の対象で計算し直します。",
                    MessageType.Warning);
            }
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(6);

            if (!string.IsNullOrEmpty(_failureMessage))
            {
                EditorGUILayout.HelpBox(_failureMessage, MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "内訳のデータがありません。[再計算] を押すと、いまの対象で計算し直します。\n"
                + "（スクリプトが再コンパイルされると内訳は破棄されます）",
                MessageType.Info);
        }

        /// <summary>合計行。「テクスチャ 合計 141.8 MB ／ ポリゴン 合計 123,247」（W0 設計 §11.2）。</summary>
        private void DrawTotalsLine()
        {
            EditorGUILayout.LabelField(
                $"テクスチャ 合計 {_breakdown.TotalTextureMegabytes:0.0} MB ／ "
                + $"ポリゴン 合計 {_breakdown.TotalPolyCount:N0}");
        }

        /// <summary>
        /// まとまり単位の1つの表。列見出しの下に、まとまり（Foldout・太字）とその中のパーツを並べる。
        /// </summary>
        private void DrawTable()
        {
            DrawColumnHeaders();
            EditorGUILayout.Space(2);

            foreach (var group in _breakdown.Groups)
            {
                DrawGroupRow(group);
            }
        }

        private void DrawColumnHeaders()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty);
            EditorGUILayout.LabelField(
                "外すと減る量", EditorStyles.miniLabel, GUILayout.Width(TextureColumnWidth + PolyColumnWidth));
            GUILayout.Space(SelectButtonWidth);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty);
            EditorGUILayout.LabelField("テクスチャ", EditorStyles.miniLabel, GUILayout.Width(TextureColumnWidth));
            EditorGUILayout.LabelField("ポリゴン", EditorStyles.miniLabel, GUILayout.Width(PolyColumnWidth));
            GUILayout.Space(SelectButtonWidth);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// まとまり1件分の行。アバター本体は外す対象ではないため、値欄と[選択]を出さない
        /// （見出しに理由を書き添える・W0 設計 §11.2）。
        /// </summary>
        private void DrawGroupRow(PerformancePartGroup group)
        {
            var key = group.Name;
            var wasOpen = !IsGroupCollapsed(key);

            EditorGUILayout.BeginHorizontal();

            var label = group.IsAvatarBody
                ? $"{group.Name}（外す対象ではないので、パーツごとの値だけ出します）"
                : group.Name;
            var nowOpen = EditorGUILayout.Foldout(wasOpen, label, true, GetBoldFoldoutStyle());

            if (!group.IsAvatarBody)
            {
                EditorGUILayout.LabelField(
                    FormatTextureValue(group.TextureValue, group.TextureReductionMegabytes),
                    GUILayout.Width(TextureColumnWidth));
                EditorGUILayout.LabelField(group.PolyCount.ToString("N0"), GUILayout.Width(PolyColumnWidth));

                // まとまりを外す実体が無い（Root が null）ケースは今のところ無いはずだが、
                // 念のため無効化だけしておく（パーツ側の Renderer null 対応と同じ考え方）
                using (new EditorGUI.DisabledScope(group.Root == null))
                {
                    if (GUILayout.Button("選択", EditorStyles.miniButton, GUILayout.Width(SelectButtonWidth)))
                    {
                        // Selection の変更だけでは Hierarchy がその位置まで動かないため PingObject を併用する
                        // （CLAUDE.md 実装ノート）
                        Selection.activeObject = group.Root;
                        EditorGUIUtility.PingObject(group.Root);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            SetGroupCollapsed(key, !nowOpen);
            if (!nowOpen) return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var part in group.Parts)
                {
                    DrawPartRow(part);
                }
            }
        }

        private static void DrawPartRow(PerformancePart part)
        {
            EditorGUILayout.BeginHorizontal();

            // 同名のパーツが複数ありうるので、識別できるようツールチップに Hierarchy の全パスを入れる
            EditorGUILayout.LabelField(new GUIContent(part.Name, part.HierarchyPath));
            EditorGUILayout.LabelField(
                FormatTextureValue(part.TextureValue, part.TextureReductionMegabytes),
                GUILayout.Width(TextureColumnWidth));
            EditorGUILayout.LabelField(part.PolyCount.ToString("N0"), GUILayout.Width(PolyColumnWidth));

            // スナップショットを取ったあとにパーツが消えていることがあるので、その行のボタンだけ無効化する
            using (new EditorGUI.DisabledScope(part.Renderer == null))
            {
                if (GUILayout.Button("選択", EditorStyles.miniButton, GUILayout.Width(SelectButtonWidth)))
                {
                    Selection.activeObject = part.Renderer.gameObject;
                    EditorGUIUtility.PingObject(part.Renderer.gameObject);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 「外すと減る量」欄の文字列化。テストしやすいよう純粋関数として切り出す。
        /// </summary>
        internal static string FormatTextureValue(TextureValueKind kind, float megabytes)
        {
            switch (kind)
            {
                case TextureValueKind.Amount:
                    return $"{megabytes:0.0} MB";
                case TextureValueKind.Shared:
                    return "共有";
                case TextureValueKind.NoTexture:
                    return "テクスチャなし";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 注記3つ。畳まずに常時表示する（DEC-094 が「ユーザーがどう受け取るかは未検証」とした点への当座の手当て）。
        /// </summary>
        private static void DrawNotes()
        {
            EditorGUILayout.HelpBox(
                "共有: 他のパーツと絵柄を使い回しているため、そのパーツだけ消しても減りません。",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "内訳の合計は総量より小さくなります。複数のまとまりが共有している分を、どこにも足していないためです。",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "この数値は、いま Unity に読み込まれているテクスチャの形式から計算しています。"
                + "ビルドターゲットを切り替えると変わります。",
                MessageType.None);
        }

        private bool IsGroupCollapsed(string key)
        {
            return _collapsedGroups != null && _collapsedGroups.Contains(key);
        }

        private void SetGroupCollapsed(string key, bool collapsed)
        {
            _collapsedGroups ??= new HashSet<string>();
            if (collapsed)
            {
                _collapsedGroups.Add(key);
            }
            else
            {
                _collapsedGroups.Remove(key);
            }
        }

        private GUIStyle GetBoldFoldoutStyle()
        {
            if (_boldFoldoutStyle == null)
            {
                _boldFoldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }
            return _boldFoldoutStyle;
        }

        /// <summary>
        /// 内訳を計算し直す。ウィンドウを開いたときと [再計算] を押したときにだけ呼ぶ
        /// （<c>RunChecks()</c> では計算しない・W0 設計 §6）。
        /// まとまりの開閉状態（<see cref="_collapsedGroups"/>）はここでは触らない（W0 設計 §11.2）。
        /// </summary>
        private void Recalculate()
        {
            _breakdown = null;
            _failureMessage = null;

            if (_target == null)
            {
                _failureMessage = "対象のアバターが指定されていません。"
                    + "おまもり本体でアバターを指定してから [再計算] を押してください。";
                return;
            }

            if (!PerformanceBreakdownBuilder.TryBuild(_target, out var breakdown))
            {
                _failureMessage = "このアバターの内訳を計算できませんでした。"
                    + "メッシュを持つパーツが無いか、VRChat SDK の構成が想定と違う可能性があります。";
                return;
            }

            _breakdown = breakdown;

            Repaint();
        }

        /// <summary>
        /// 開いている主画面が今どのアバターを対象にしているかを読む。
        /// 主画面が閉じている場合は null を返し、対象ずれの警告は出さない。
        /// </summary>
        private static GameObject FindMainWindowTarget()
        {
            var windows = Resources.FindObjectsOfTypeAll<AvatarOmamoriWindow>();
            foreach (var window in windows)
            {
                if (window == null) continue;
                var root = window.CurrentAvatarRoot;
                if (root != null) return root;
            }
            return null;
        }
    }
}
