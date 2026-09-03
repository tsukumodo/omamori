using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// パーツ別のパフォーマンス内訳を表示する EditorWindow（v0.11.0 W0 設計 §2 / DEC-100）。
    ///
    /// <para>
    /// 主画面には「どのパーツが重いか見る」ボタン1行だけを足し、内訳はこのウィンドウに出す。
    /// 主画面へインライン展開すると縦幅が 200〜250px 増え、DEC-097 が示した
    /// 「再燃したら情報量を削る」方向と逆行するため（制約 C3）。
    /// </para>
    /// <para>
    /// プラットフォーム（PC / Quest）では分けない。<c>AnalyzeMaterials</c> はビルドターゲットを
    /// 一切参照しないと IL 解析で確定しており、内訳は PC / Quest で必ず同一になる（W0 設計 §2.5）。
    /// 「Quest の内訳」と名乗らせないことも含めて、プラットフォーム名はこのウィンドウに出さない。
    /// </para>
    /// <para>
    /// 表示は差分方式（「このパーツを消すと ○MB 減ります」）。単独帰属にすると共有テクスチャが
    /// 多重計上され、合計が実際の数倍に水増しされる（8/8 実測: 850.3MB vs 実際 336.8MB）。
    /// 代わりに「消しても減らない」パーツが多数派になりうるので、そこを例外の注記ではなく
    /// 一級の説明として書く（TestAvatar では 27件中24件が該当）。
    /// </para>
    /// </summary>
    internal sealed class PerformanceBreakdownWindow : EditorWindow
    {
        private const string WindowTitle = "おまもり — パフォーマンスの内訳";
        private const float SelectButtonWidth = 48f;
        private const float ValueColumnWidth = 190f;

        // 対象は UnityEngine.Object 参照なのでドメインリロードをまたいで復元されるが、
        // スナップショットは [NonSerialized] で捨てる。寿命を分けているのは意図的で、
        // 「リロード後は対象を覚えたまま、中身だけ [再計算] を促す」状態にするため。
        // 復元済みかどうかの判定は null ではなく件数で見る（CLAUDE.md 実装ノートの教訓）。
        [SerializeField] private GameObject _target;
        [SerializeField] private bool _foldZeroReduction;

        [NonSerialized] private PerformanceBreakdown _breakdown;
        [NonSerialized] private List<PerformancePart> _byPolyCount;
        [NonSerialized] private string _failureMessage;

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
            if (_breakdown == null || _breakdown.Parts.Count == 0)
            {
                DrawEmptyState();
                EditorGUILayout.Space(8);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(8);
            DrawTextureSection();

            EditorGUILayout.Space(10);
            DrawPolySection();

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

        /// <summary>
        /// テクスチャ使用量の内訳。減る量の降順に全件出し、0 MB のパーツは末尾にまとめる。
        /// </summary>
        private void DrawTextureSection()
        {
            var reducible = _breakdown.Parts.Where(p => p.TextureReductionMegabytes > 0f).ToList();
            var shared = _breakdown.Parts.Where(p => p.TextureReductionMegabytes <= 0f).ToList();

            EditorGUILayout.LabelField(
                "テクスチャ使用量",
                $"合計 {_breakdown.TotalTextureMegabytes:0.0} MB",
                EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var part in reducible)
                {
                    DrawPartRow(part, $"消すと {part.TextureReductionMegabytes:0.0} MB 減ります");
                }

                if (reducible.Count == 0 && shared.Count > 0)
                {
                    EditorGUILayout.LabelField(
                        "単体で消して減るパーツはありませんでした。すべて他のパーツと絵柄を共有しています。",
                        EditorStyles.wordWrappedLabel);
                }

                if (shared.Count == 0) return;

                EditorGUILayout.Space(2);
                _foldZeroReduction = EditorGUILayout.Foldout(
                    _foldZeroReduction,
                    $"他のパーツと絵柄を共有しているため、単体で消しても減らないもの（{shared.Count}件）",
                    true);

                if (!_foldZeroReduction) return;

                EditorGUILayout.LabelField(
                    "このアバターは絵柄をよく使い回しています。ここに並ぶパーツは、個別に消しても軽くなりません。",
                    EditorStyles.wordWrappedLabel);

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var part in shared)
                    {
                        DrawPartRow(part, "消しても減りません");
                    }
                }
            }
        }

        /// <summary>
        /// ポリゴン数の内訳。テクスチャと違い共有が起きないため、常に実数が降順に並ぶ。
        /// テクスチャ側が全部 0 MB のアバターでも、こちらが主役として機能する。
        /// </summary>
        private void DrawPolySection()
        {
            EditorGUILayout.LabelField(
                "ポリゴン数",
                $"合計 {_breakdown.TotalPolyCount:N0}",
                EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var part in _byPolyCount)
                {
                    DrawPartRow(part, part.PolyCount.ToString("N0"));
                }
            }
        }

        /// <summary>
        /// 注記2つ。畳まずに常時表示する（DEC-094 が「ユーザーがどう受け取るかは未検証」とした点への当座の手当て）。
        /// </summary>
        private static void DrawNotes()
        {
            EditorGUILayout.HelpBox(
                "内訳の合計は総量より小さくなります。複数のパーツが共有している分を、どのパーツにも足していないためです。",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "この数値は、いま Unity に読み込まれているテクスチャの形式から計算しています。"
                + "ビルドターゲットを切り替えると変わります。",
                MessageType.None);
        }

        private static void DrawPartRow(PerformancePart part, string valueText)
        {
            EditorGUILayout.BeginHorizontal();

            // 同名のパーツが複数ありうるので、識別できるようツールチップに Hierarchy の全パスを入れる
            EditorGUILayout.LabelField(new GUIContent(part.Name, part.HierarchyPath));
            EditorGUILayout.LabelField(valueText, GUILayout.Width(ValueColumnWidth));

            // スナップショットを取ったあとにパーツが消えていることがあるので、その行のボタンだけ無効化する
            using (new EditorGUI.DisabledScope(part.Renderer == null))
            {
                if (GUILayout.Button("選択", EditorStyles.miniButton, GUILayout.Width(SelectButtonWidth)))
                {
                    // Selection の変更だけでは Hierarchy がその位置まで動かないため PingObject を併用する
                    // （CLAUDE.md 実装ノート）
                    Selection.activeObject = part.Renderer.gameObject;
                    EditorGUIUtility.PingObject(part.Renderer.gameObject);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 内訳を計算し直す。ウィンドウを開いたときと [再計算] を押したときにだけ呼ぶ
        /// （<c>RunChecks()</c> では計算しない・W0 設計 §6）。
        /// </summary>
        private void Recalculate()
        {
            _breakdown = null;
            _byPolyCount = null;
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
            _byPolyCount = breakdown.Parts.OrderByDescending(p => p.PolyCount).ToList();

            // 0 MB のパーツが多数派になるアバターがある（TestAvatar では 27件中24件）。
            // 畳んだままだとほぼ空のウィンドウに見えて壊れて見えるので、多数派のときは既定で開く（W0 設計 §2.5）。
            var sharedCount = breakdown.Parts.Count(p => p.TextureReductionMegabytes <= 0f);
            _foldZeroReduction = sharedCount > breakdown.Parts.Count - sharedCount;

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
