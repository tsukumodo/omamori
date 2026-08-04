using System;
using System.Collections.Generic;
using System.Linq;
using AvatarOmamori.Editor.Performance;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// アバター改変おまもりのメイン EditorWindow。
    /// アバタールートを指定してチェックを実行し、結果を Severity 別に表示する。
    /// </summary>
    public sealed class AvatarOmamoriWindow : EditorWindow
    {
        private GameObject _avatarRoot;
        private List<CheckResult> _results;
        private List<CheckResult> _errors;
        private List<CheckResult> _warnings;
        private List<CheckResult> _infos;
        private Vector2 _scrollPos;
        private bool _foldError = true;
        private bool _foldWarning = true;
        private bool _foldInfo = true;
        private bool _foldHistory = false; // 履歴はデフォルト閉じる（結果カードを優先）

        // パフォーマンス表示（v0.9.0）。ランクは「問題」ではないので Error/Warning/Info の件数には含めず、
        // 専用セクションで PC / Quest を並べて見せる（DEC-070）。
        private AvatarPerformanceReport _performanceReport;
        private bool _foldPerformance = true; // 主役機能なので既定は展開

        // 「ほか N 件を見る」で展開済みのプラットフォーム。
        // [NonSerialized] が必須。EditorWindow はドメインリロード（スクリプト再コンパイル）をまたいで
        // 状態を復元する際、シリアライズ対象外の参照フィールドを null にして戻すことがあるため、
        // 参照する側でも必ず EnsureExpandedFactors() を通す。
        [NonSerialized]
        private HashSet<PerformancePlatform> _expandedFactors;

        /// <summary>常時表示する要因の件数。残りは「ほか N 件を見る」で展開する（DEC-070）。</summary>
        private const int VisibleFactorCount = 3;

        /// <summary>Severity アイコンの表示幅。内訳行では同じ幅を空けて本文の開始位置を揃える。</summary>
        private const float SeverityIconWidth = 20f;

        /// <summary>内訳行（<see cref="CheckResult.IsDetail"/>）の字下げ幅。</summary>
        private const float DetailIndentWidth = 16f;

        // 「カードを保存」で選ばれた出力先。GL 描画は Repaint イベント中に行う必要があるため、
        // ボタン押下時はパスだけ確保し、次の Repaint で実際の書き出しを実行する。
        //
        // [NonSerialized] が必須。EditorWindow はドメインリロード（スクリプト再コンパイル）をまたいで
        // 状態を復元するが、その際 null の string は "" に変換されて戻ってくる（実測で確認・2026-07-23）。
        // これを付けないと、ボタンを押していないのに再コンパイル直後の Repaint で書き出しが走る。
        [NonSerialized]
        private string _pendingCardSavePath;

        // GUIStyle のキャッシュ
        private GUIStyle _summaryStyle;
        private GUIStyle _foldoutStyle;
        private GUIStyle _ratingStyle;

        [MenuItem("Tools/アバター改変おまもり")]
        public static void ShowWindow()
        {
            GetWindow<AvatarOmamoriWindow>("アバター改変おまもり");
        }

        /// <summary>
        /// 現在のアバタールートに対してチェックを再実行し、UI を更新する。
        /// FixAction が非同期的な処理（選択ドロップダウン等）を含み、完了のタイミングを
        /// 共通基盤側で知れない場合、FixAction 側から明示的に呼び出して UI を最新状態に戻す。
        /// </summary>
        public void RefreshResults()
        {
            if (_avatarRoot == null) return;
            RunChecks();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("アバター改変おまもり", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 初回のみ、利用統計のローカル記録について1行で告知する（ダイアログは出さない＝押し売りゼロ・DEC-055 T-8）
            if (UsageStatsRecorder.ShouldShowFirstRunNotice && !UsageStatsRecorder.IsOptedOut)
            {
                DrawFirstRunNotice();
            }

            var newAvatarRoot = (GameObject)EditorGUILayout.ObjectField(
                "アバタールート", _avatarRoot, typeof(GameObject), true);
            if (newAvatarRoot != _avatarRoot)
            {
                _avatarRoot = newAvatarRoot;
                // アバターを指定した瞬間に自動チェックを走らせる。
                // オンボーディング時のボタン押し忘れを防ぎ、ツールの価値をすぐに体験してもらうため。
                if (_avatarRoot != null)
                {
                    RunChecks();
                }
                else
                {
                    // アバター参照がクリアされたら結果も消す（古い結果が残ると誤解の元）
                    _results = null;
                    _performanceReport = null;
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_avatarRoot == null))
            {
                if (GUILayout.Button("チェック実行", GUILayout.Height(30)))
                {
                    RunChecks();
                }
            }

            if (_results == null)
                return;

            EditorGUILayout.Space(4);

            // サマリー。内訳行は「1つの問題を説明する補助行」なので件数に数えない（CountPrimary）
            var summary = $"結果: {CountPrimary(_errors)} Error / {CountPrimary(_warnings)} Warning / {CountPrimary(_infos)} Info";
            var summaryStyle = GetSummaryStyle();
            // 色は表示している件数と同じ数え方で決める。生の Count で判定すると
            // 内訳行しかない Severity で「0 Error」を赤く塗ってしまう
            if (CountPrimary(_errors) > 0)
                summaryStyle.normal.textColor = new Color(0.9f, 0.2f, 0.2f);
            else if (CountPrimary(_warnings) > 0)
                summaryStyle.normal.textColor = new Color(0.9f, 0.7f, 0.1f);
            else
                summaryStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);

            EditorGUILayout.BeginHorizontal();
            // LabelField はプレフィックスラベル扱いで labelWidth に切り詰められるため GUILayout.Label を使う
            GUILayout.Label(summary, summaryStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("カードを保存", GUILayout.Width(100)))
            {
                RequestCardSave();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawPerformanceSection();

            // 表示条件は CountPrimary ではなく生の Count で見る。
            // 内訳行を1行も取りこぼさないため（見出しの件数は DrawSeverityGroup 側で CountPrimary を使う）
            if (_errors.Count > 0)
                DrawSeverityGroup("Error", _errors, ref _foldError, new Color(0.9f, 0.2f, 0.2f));
            if (_warnings.Count > 0)
                DrawSeverityGroup("Warning", _warnings, ref _foldWarning, new Color(0.9f, 0.7f, 0.1f));
            if (_infos.Count > 0)
                DrawSeverityGroup("Info", _infos, ref _foldInfo, new Color(0.5f, 0.7f, 1f));

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("問題は見つかりませんでした。", MessageType.Info);
            }

            if (FixHistoryStore.Count > 0)
            {
                EditorGUILayout.Space(6);
                DrawFixHistoryGroup();
            }

            EditorGUILayout.EndScrollView();

            // GL 描画とフォントアトラスの状態が安定している Repaint イベント中に、カードの書き出しを実行する。
            // 空文字も弾く（RequestCardSave 側のキャンセル判定と条件を揃える）。
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(_pendingCardSavePath))
            {
                var path = _pendingCardSavePath;
                _pendingCardSavePath = null;
                ExportCard(path);
            }
        }

        /// <summary>
        /// 「カードを保存」ボタンから呼ぶ。保存先を選ばせ、選ばれたら次の Repaint で書き出す。
        /// キャンセル時は何もしない。
        /// </summary>
        private void RequestCardSave()
        {
            var defaultName = $"omamori-card-{DateTime.Now:yyyyMMdd}.png";
            var path = EditorUtility.SaveFilePanel(
                "カード画像の保存先", "", defaultName, "png");
            if (string.IsNullOrEmpty(path))
                return; // キャンセル

            _pendingCardSavePath = path;
            Repaint();
        }

        /// <summary>
        /// 現在の結果サマリーをカード画像として <paramref name="path"/> に書き出し、保存先を開く。
        /// GL 描画を伴うため Repaint イベント中にのみ呼ぶこと。
        /// </summary>
        private void ExportCard(string path)
        {
            try
            {
                var data = new CardExporter.CardData
                {
                    // 画面のサマリー表示と数え方を揃える（内訳行は数えない）
                    ErrorCount = CountPrimary(_errors),
                    WarningCount = CountPrimary(_warnings),
                    InfoCount = CountPrimary(_infos),
                    FixCount = FixHistoryStore.Count,
                    DateText = DateTime.Now.ToString("yyyy-MM-dd"), // 年月日のみ（DEC-055 準拠）
                    ToolVersion = UsageStatsRecorder.GetSnapshot().ToolVersion,
                };
                CardExporter.ExportPng(path, data);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "おまもり — エラー",
                    $"カード画像の書き出しに失敗しました。\n{e.Message}",
                    "OK");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// 初回起動時の利用統計の告知（1行・helpBox 風）。
        /// 「詳細」で統計ウィンドウを開き、「OK」で閉じる。どちらを押しても次回以降は表示しない。
        /// </summary>
        private void DrawFirstRunNotice()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "利用状況（チェック・修正の回数など。個人情報は含みません）をこのプロジェクト内にローカル記録します。",
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("詳細", GUILayout.Width(46), GUILayout.Height(20)))
            {
                UsageStatsRecorder.AcknowledgeNotice();
                UsageStatsWindow.ShowWindow();
            }
            if (GUILayout.Button("OK", GUILayout.Width(40), GUILayout.Height(20)))
            {
                UsageStatsRecorder.AcknowledgeNotice();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// 全チェックとパフォーマンス計測を実行し、表示用の状態を作り直す。
        /// </summary>
        private void RunChecks()
        {
            _results = CheckRunner.RunAll(_avatarRoot);
            CacheResultsByCategory();

            _performanceReport = PerformanceReportBuilder.Build(_avatarRoot);
            EnsureExpandedFactors().Clear(); // 再チェックのたびに「ほか N 件」は畳み直す
            RecordPerformanceUsage(_performanceReport);
        }

        /// <summary>
        /// チェック結果を Severity 別に分類してキャッシュする。
        /// </summary>
        private void CacheResultsByCategory()
        {
            _errors = _results.Where(r => r.Severity == Severity.Error).ToList();
            _warnings = _results.Where(r => r.Severity == Severity.Warning).ToList();
            _infos = _results.Where(r => r.Severity == Severity.Info).ToList();
        }

        /// <summary>
        /// 件数表示に使う「問題の数」を数える。
        /// 内訳行（<see cref="CheckResult.IsDetail"/>）は1つの問題を補足する行なので数えない。
        /// リスト自体は表示のため全件保持したままにする。
        /// </summary>
        internal static int CountPrimary(List<CheckResult> items)
            => items.Count(r => !r.IsDetail);

        private GUIStyle GetSummaryStyle()
        {
            if (_summaryStyle == null)
            {
                _summaryStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold
                };
            }
            return _summaryStyle;
        }

        /// <summary>
        /// 総合ランク表示用のスタイル。色だけ呼び出しごとに差し替える。
        /// OnGUI 内で GUIStyle を new すると Repaint のたびにアロケーションが発生するため、
        /// このファイルの他のスタイルと同じくキャッシュする。
        /// </summary>
        private GUIStyle GetRatingStyle(bool isHeavy)
        {
            if (_ratingStyle == null)
            {
                _ratingStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }

            _ratingStyle.normal.textColor = isHeavy
                ? new Color(0.9f, 0.7f, 0.1f)
                : new Color(0.35f, 0.75f, 0.45f);
            return _ratingStyle;
        }

        private GUIStyle GetFoldoutStyle()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };
            }
            return _foldoutStyle;
        }

        private void DrawSeverityGroup(string label, List<CheckResult> items, ref bool foldout, Color color)
        {
            var style = GetFoldoutStyle();
            style.normal.textColor = color;
            style.onNormal.textColor = color;

            foldout = EditorGUILayout.Foldout(foldout, $"{label} ({CountPrimary(items)})", true, style);
            if (!foldout)
                return;

            EditorGUI.indentLevel++;
            foreach (var result in items)
            {
                // 内訳行は1段字下げして、直前のサマリー行にぶら下がっていることを見た目で示す
                if (result.IsDetail)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(DetailIndentWidth);
                }

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                // Severity アイコン。内訳行では出さず（同じ赤アイコンが並ぶと問題が複数あるように見えるため）、
                // 代わりに同じ幅だけ空けて本文の開始位置をサマリー行と揃える
                if (result.IsDetail)
                {
                    GUILayout.Space(SeverityIconWidth);
                }
                else
                {
                    var iconContent = GetSeverityIcon(result.Severity);
                    if (iconContent != null)
                    {
                        GUILayout.Label(iconContent, GUILayout.Width(SeverityIconWidth), GUILayout.Height(20));
                    }
                }

                EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedLabel);

                if (result.TargetObject != null)
                {
                    if (GUILayout.Button("選択", GUILayout.Width(40)))
                    {
                        EditorGUIUtility.PingObject(result.TargetObject);
                        Selection.activeObject = result.TargetObject;
                    }
                }

                if (result.HasFix)
                {
                    var fixLabel = result.FixLabel ?? "修正";
                    // デフォルト「修正」のときは「選択」ボタンと幅を揃え、カスタムラベル指定時のみ内容に合わせて広げる
                    var fixWidth = result.FixLabel == null
                        ? 40f
                        : GUI.skin.button.CalcSize(new GUIContent(fixLabel)).x + 8f;
                    if (GUILayout.Button(fixLabel, GUILayout.Width(fixWidth)))
                    {
                        var capturedResult = result;
                        if (capturedResult.SkipConfirm)
                        {
                            // FixAction 側で独自ウィンドウを出す項目（例: Descriptor 重複の選択ウィンドウ）。
                            // 事前確認は出さず、再チェックも FixAction 側が完了時に RefreshResults() を呼ぶ責任を持つ。
                            ExecuteFix(capturedResult, refreshAfter: false);
                        }
                        else
                        {
                            var msg = capturedResult.FixConfirmMessage ?? "この問題を自動修正しますか？\nUndo（Ctrl+Z）で元に戻せます。";
                            OmamoriConfirmWindow.Show(
                                title: "おまもり — 自動修正",
                                message: msg,
                                okLabel: "修正する",
                                cancelLabel: "キャンセル",
                                onOk: () => ExecuteFix(capturedResult, refreshAfter: true));
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(result.ValueLabel) &&
                    (result.BeforeValue != null || result.AfterValue != null))
                {
                    EditorGUILayout.BeginHorizontal();
                    // アイコン幅(20)+α + 上位の indentLevel 分(15px×段数) を加算してメッセージ本文の下に揃える
                    GUILayout.Space(24f + EditorGUI.indentLevel * 15f);
                    OmamoriPopupStyles.DrawValueSnapshot(
                        result.ValueLabel, result.BeforeValue, result.AfterValue);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();

                if (result.IsDetail)
                {
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(2);
            }
            EditorGUI.indentLevel--;
        }

        // ───────────────────────────── パフォーマンス表示（v0.9.0 / DEC-070） ─────────────────────────────

        /// <summary>ブランドの金色。パフォーマンスセクションの見出しに使い、Severity の色と混同させない。</summary>
        private static readonly Color AccentGold = new Color(0.784f, 0.659f, 0.486f);

        /// <summary>
        /// PC / Quest の総合ランクと、ランクを下げている要因を表示する（DEC-070）。
        /// ランクは「見つかった問題」ではないため、結果サマリーの件数には含めない。
        /// </summary>
        private void DrawPerformanceSection()
        {
            if (_performanceReport == null) return;

            var foldoutStyle = GetFoldoutStyle();
            foldoutStyle.normal.textColor = AccentGold;
            foldoutStyle.onNormal.textColor = AccentGold;

            _foldPerformance = EditorGUILayout.Foldout(_foldPerformance, "パフォーマンス", true, foldoutStyle);
            if (!_foldPerformance) return;

            if (!_performanceReport.IsValid)
            {
                EditorGUILayout.HelpBox(_performanceReport.FailureReason, MessageType.Info);
                return;
            }

            DrawPlatformBlock(_performanceReport.Pc);
            DrawPlatformBlock(_performanceReport.Quest, _performanceReport.QuestIncompatibilities);

            // 注記は折りたたみの中に隠さず常時表示する（DEC-070）
            EditorGUILayout.HelpBox(
                "ここの数値は Modular Avatar などの非破壊改変ツールを適用する前のものです。"
                + "最終的な値は VRChat SDK のビルドパネルでご確認ください。",
                MessageType.None);
            EditorGUILayout.Space(4);
        }

        private void DrawPlatformBlock(
            PlatformPerformance platform, IReadOnlyList<QuestIncompatibility> incompatibilities = null)
        {
            if (platform == null || !platform.IsValid) return;

            var isQuest = platform.Platform == PerformancePlatform.Quest;
            var title = isQuest ? "Quest / iOS" : "PC";

            EditorGUILayout.BeginVertical("box");

            // 見出し行: プラットフォーム名 ─ 総合ランク
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                platform.OverallRatingName, GetRatingStyle(platform.IsHeavy), GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            // 説明文。ランクを突きつけず「上げると何が良くなるか」を書く（DEC-069 決定事項7）
            EditorGUILayout.LabelField(BuildPlatformLead(platform), EditorStyles.wordWrappedMiniLabel);

            DrawFactors(platform);

            if (incompatibilities != null && incompatibilities.Count > 0)
            {
                // Quest 固有の話と、PC でも剥がされる話を混ぜない。
                // 混ぜると PC しか使わない人が後者を「Quest の話」として読み飛ばす
                DrawIncompatibilityGroup(
                    "Quest では使えないもの", incompatibilities, IncompatibilityScope.Quest);
                DrawIncompatibilityGroup(
                    "アップロード時に取り除かれるもの（PC / Quest 共通）",
                    incompatibilities, IncompatibilityScope.AllPlatforms);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        /// <summary>プラットフォームごとの導入文。</summary>
        private static string BuildPlatformLead(PlatformPerformance platform)
        {
            var isQuest = platform.Platform == PerformancePlatform.Quest;

            if (platform.Factors.Count == 0)
            {
                // 要因が空になる理由は2通りある。ランクが低いのに「軽く動きます」と書くと矛盾するため、
                // 総合ランクが最高かどうかで文言を分ける（AABB・パーティクル設定などは内訳の対象外）
                if (!platform.IsBestRating)
                {
                    return "ランクを下げている項目を、この画面では特定できませんでした。"
                           + "アバターの大きさやパーティクルの設定が原因のことがあります。"
                           + "詳しくは VRChat SDK のビルドパネルでご確認ください。";
                }

                return isQuest
                    ? "Quest / iOS でも軽く動きます。"
                    : "PC では軽く動きます。";
            }

            return isQuest
                ? "Quest / iOS では Medium 以上にすると、既定で他の人から見えるようになります。"
                  + "下の項目をすべて次のランクの範囲まで下げると、総合ランクが1つ上がります。"
                : "下の項目をすべて次のランクの範囲まで下げると、総合ランクが1つ上がります。";
        }

        /// <summary>
        /// ランクを下げている要因を、改善効果の大きい順に表示する。
        /// 常時表示は上位 <see cref="VisibleFactorCount"/> 件で、残りは「ほか N 件を見る」で展開（DEC-070）。
        /// </summary>
        private void DrawFactors(PlatformPerformance platform)
        {
            if (platform.Factors.Count == 0) return;

            var expandedFactors = EnsureExpandedFactors();
            var expanded = expandedFactors.Contains(platform.Platform);
            var shownCount = expanded ? platform.Factors.Count : Math.Min(VisibleFactorCount, platform.Factors.Count);

            for (var i = 0; i < shownCount; i++)
            {
                DrawFactorRow(platform.Factors[i]);
            }

            var hiddenCount = platform.Factors.Count - shownCount;
            if (hiddenCount > 0)
            {
                if (GUILayout.Button($"ほか {hiddenCount} 件を見る", EditorStyles.miniButton))
                {
                    expandedFactors.Add(platform.Platform);
                }
            }
            else if (expanded && platform.Factors.Count > VisibleFactorCount)
            {
                if (GUILayout.Button("折りたたむ", EditorStyles.miniButton))
                {
                    expandedFactors.Remove(platform.Platform);
                }
            }
        }

        /// <summary>
        /// 展開状態のセットを返す。ドメインリロード後に null になっていても作り直す。
        /// </summary>
        private HashSet<PerformancePlatform> EnsureExpandedFactors()
        {
            return _expandedFactors ?? (_expandedFactors = new HashSet<PerformancePlatform>());
        }

        private void DrawFactorRow(PerformanceFactor factor)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"・{factor.Label}", GUILayout.Width(200));

            var valueText = factor.TargetText == null
                ? factor.CurrentText
                : $"{factor.CurrentText} → {factor.TargetText} まで（{factor.TargetRatingName}）";
            EditorGUILayout.LabelField(valueText, EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrEmpty(factor.DocumentUrl))
            {
                if (GUILayout.Button("調べる", EditorStyles.miniButton, GUILayout.Width(56)))
                {
                    Application.OpenURL(factor.DocumentUrl);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 指定した <paramref name="scope"/> の項目だけを見出し付きで描画する。
        /// 該当が1件も無ければ見出しごと出さない。
        /// </summary>
        private void DrawIncompatibilityGroup(
            string heading, IReadOnlyList<QuestIncompatibility> items, IncompatibilityScope scope)
        {
            var matched = items.Where(i => i.Scope == scope).ToList();
            if (matched.Count == 0) return;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);
            foreach (var item in matched)
            {
                DrawIncompatibility(item);
            }
        }

        private void DrawIncompatibility(QuestIncompatibility item)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                $"・{item.Label}（{item.Count} 件）{item.Detail}", EditorStyles.wordWrappedLabel);

            if (item.Targets.Count > 0)
            {
                if (GUILayout.Button("選択", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    Selection.objects = item.Targets.ToArray();
                    EditorGUIUtility.PingObject(item.Targets[0]);
                }
            }

            // 案内先が無い項目（PC / Quest 共通の非対応コンポーネントなど）ではボタンを出さない。
            // Quest のドキュメントを開かせると、Quest の話だと誤解させてしまう
            if (!string.IsNullOrEmpty(item.DocumentUrl)
                && GUILayout.Button("調べる", EditorStyles.miniButton, GUILayout.Width(56)))
            {
                Application.OpenURL(item.DocumentUrl);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// パフォーマンス表示の発火を利用統計に記録する（DEC-069 T-7）。
        /// キーは ASCII 識別子のみ。アバター名・パス等は一切渡さない（DEC-055 の収集境界を維持）。
        /// </summary>
        private static void RecordPerformanceUsage(AvatarPerformanceReport report)
        {
            if (report == null || !report.IsValid) return;

            var counts = new Dictionary<string, int>();
            if (report.Pc != null && report.Pc.IsValid) counts["performance_rank_pc"] = 1;
            if (report.Quest != null && report.Quest.IsValid) counts["performance_rank_quest"] = 1;
            if (report.QuestIncompatibilities.Count > 0)
                counts["quest_incompatibility"] = report.QuestIncompatibilities.Count;

            if (counts.Count == 0) return;

            // CheckRunner.RunAll と同じ実行で呼ばれるため、実行回数を二重計上しない入口を使う
            UsageStatsRecorder.RecordDetections(counts);
        }

        /// <summary>
        /// セッション内の修正履歴（<see cref="FixHistoryStore"/>）を Foldout で表示する。
        /// 新しい順に1件ずつ、修正対象の Ping ボタン付き。履歴クリアボタンは Foldout ヘッダ右端。
        /// </summary>
        private void DrawFixHistoryGroup()
        {
            var foldoutStyle = GetFoldoutStyle();
            foldoutStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            foldoutStyle.onNormal.textColor = new Color(0.6f, 0.6f, 0.6f);

            EditorGUILayout.BeginHorizontal();
            _foldHistory = EditorGUILayout.Foldout(
                _foldHistory, $"修正履歴 ({FixHistoryStore.Count})", true, foldoutStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("クリア", GUILayout.Width(60)))
            {
                FixHistoryStore.Clear();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            if (!_foldHistory) return;

            EditorGUI.indentLevel++;
            foreach (var entry in FixHistoryStore.Entries)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                var header = $"[{entry.Timestamp:HH:mm:ss}] {entry.TargetObjectName}";
                EditorGUILayout.LabelField(header, EditorStyles.miniLabel);
                var target = EditorUtility.InstanceIDToObject(entry.TargetInstanceID);
                using (new EditorGUI.DisabledScope(target == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(40)))
                    {
                        EditorGUIUtility.PingObject(target);
                        Selection.activeObject = target;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4f + EditorGUI.indentLevel * 15f);
                OmamoriPopupStyles.DrawValueSnapshot(
                    entry.ValueLabel, entry.BeforeValue, entry.AfterValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// FixAction を安全に実行する。
        /// 同期的な修正は <paramref name="refreshAfter"/>=true で直後に再チェックする。
        /// 非同期的な修正（内部で独自 UI を開くもの）は <paramref name="refreshAfter"/>=false で呼び、
        /// FixAction 側が完了時に <see cref="RefreshResults"/> を呼ぶ責任を持つ。
        /// </summary>
        private void ExecuteFix(CheckResult result, bool refreshAfter)
        {
            try
            {
                result.FixAction();
                if (refreshAfter)
                {
                    RefreshResults();
                }
            }
            catch (ExitGUIException)
            {
                // Unity の GUI システムが「現フレームの GUI 処理を中断する」ために投げる正常動作の例外
                // （PopupWindow.Show 等が投げる）。アプリレベルのエラーではないので Unity に委ねる。
                throw;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "おまもり — エラー",
                    $"修正中にエラーが発生しました。\n{e.Message}",
                    "OK");
            }
        }

        private static GUIContent GetSeverityIcon(Severity severity)
        {
            switch (severity)
            {
                case Severity.Error:
                    return EditorGUIUtility.IconContent("console.erroricon.sml");
                case Severity.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon.sml");
                case Severity.Info:
                    return EditorGUIUtility.IconContent("console.infoicon.sml");
                default:
                    return null;
            }
        }
    }
}
