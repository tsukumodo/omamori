using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// 蓄積した利用統計（<see cref="UsageStatsRecorder"/>）を閲覧・操作する EditorWindow。
    /// 表示／「フィードバックとしてコピー」／「統計をクリア」／「収集を無効化・再開」を提供する（DEC-055）。
    /// データはローカル完結で、本ウィンドウからの操作以外でデータが外部へ出ることはない。
    /// </summary>
    public sealed class UsageStatsWindow : EditorWindow
    {
        // つくも堂 X（@tsukumodo_lab）プロフィール。コピー後に DM 導線として開く。
        private const string TsukumodoXUrl = "https://x.com/tsukumodo_lab";

        private Vector2 _scroll;

        [MenuItem("Tools/つくも堂/使用統計を見る")]
        public static void ShowWindow()
        {
            var window = GetWindow<UsageStatsWindow>("つくも堂 — 使用統計");
            window.minSize = new Vector2(460f, 520f);
        }

        private void OnGUI()
        {
            var stats = UsageStatsRecorder.GetSnapshot();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("つくも堂 — 使用統計", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // プライバシー説明（収集する / しない の境界を明示）
            OmamoriPopupStyles.DrawInfoBox(
                "この統計はこのプロジェクト内（Library/）にローカル保存され、自動送信は一切しません。\n" +
                "収集する: チェック・修正の種別ごとの回数 / 実行回数 / 日付 / ツールバージョン\n" +
                "収集しない: アバター名・パス・シーン名・PC名・ユーザー名・時分秒");

            if (stats.OptOut)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox("現在、利用統計の収集は無効になっています。", MessageType.Warning);
            }

            EditorGUILayout.Space(6);
            DrawSummary(stats);

            EditorGUILayout.Space(6);
            DrawCountSection("検出（チェック種別ごとの累計件数）", stats.DetectionCounts);

            EditorGUILayout.Space(6);
            DrawCountSection("自動修正（種別ごとの累計実行回数）", stats.FixRunCounts);

            EditorGUILayout.Space(10);
            DrawActions(stats);

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSummary(UsageStats stats)
        {
            EditorGUILayout.LabelField("概要", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("ツールバージョン", Display(stats.ToolVersion));
                EditorGUILayout.LabelField("初回記録日", Display(stats.FirstRun));
                EditorGUILayout.LabelField("最終記録日", Display(stats.LastRun));
                EditorGUILayout.LabelField("チェック実行回数", stats.CheckRunCount.ToString());
                EditorGUILayout.LabelField("最終エクスポート",
                    string.IsNullOrEmpty(stats.LastExportedAt) ? "未エクスポート" : stats.LastExportedAt);
            }
        }

        private static void DrawCountSection(string title, Dictionary<string, int> counts)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                if (counts == null || counts.Count == 0)
                {
                    EditorGUILayout.LabelField("まだ記録がありません。");
                    return;
                }

                // 多い順 → キー昇順で安定表示
                var ordered = new List<KeyValuePair<string, int>>(counts);
                ordered.Sort((a, b) =>
                {
                    int byCount = b.Value.CompareTo(a.Value);
                    return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
                });

                foreach (var kv in ordered)
                {
                    EditorGUILayout.LabelField(kv.Key, kv.Value.ToString());
                }
            }
        }

        private void DrawActions(UsageStats stats)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("フィードバックとしてコピー", GUILayout.Height(28)))
            {
                CopyAsFeedback();
            }

            if (GUILayout.Button("統計をクリア", GUILayout.Height(28), GUILayout.Width(110)))
            {
                ClearStats();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 収集の有効 / 無効トグル
            if (stats.OptOut)
            {
                if (GUILayout.Button("収集を再開する", GUILayout.Height(24)))
                {
                    UsageStatsRecorder.SetOptOut(false);
                    Repaint();
                }
            }
            else
            {
                if (GUILayout.Button("収集を無効化する", GUILayout.Height(24)))
                {
                    UsageStatsRecorder.SetOptOut(true);
                    Repaint();
                }
            }
        }

        /// <summary>
        /// JSON をクリップボードへコピーし、つくも堂 X プロフィールを開いて DM 導線を案内する。
        /// データを自動送信するのではなく、本人がコピー内容を任意で送れるようにするだけ（押し売りゼロ）。
        /// </summary>
        private void CopyAsFeedback()
        {
            var json = UsageStatsRecorder.MarkExportedAndGetJson();
            EditorGUIUtility.systemCopyBuffer = json;

            bool openX = EditorUtility.DisplayDialog(
                "おまもり — フィードバック",
                "利用統計をクリップボードにコピーしました。\n\n" +
                "もしよければ、つくも堂の X（@tsukumodo_lab）の DM に貼り付けて送っていただけると、" +
                "今後の改善の参考になります。送るかどうかは自由です。\n\n" +
                "つくも堂の X を開きますか？",
                "Xを開く",
                "閉じる");

            if (openX)
            {
                Application.OpenURL(TsukumodoXUrl);
            }
            Repaint();
        }

        private void ClearStats()
        {
            bool ok = EditorUtility.DisplayDialog(
                "おまもり — 統計をクリア",
                "蓄積した利用統計を消去します。よろしいですか？\n（収集の有効/無効の設定は保持されます）",
                "クリアする",
                "キャンセル");
            if (ok)
            {
                UsageStatsRecorder.ClearStats();
                Repaint();
            }
        }

        private static string Display(string value) => string.IsNullOrEmpty(value) ? "—" : value;
    }
}
