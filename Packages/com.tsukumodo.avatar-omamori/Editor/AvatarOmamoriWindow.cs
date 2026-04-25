using System;
using System.Collections.Generic;
using System.Linq;
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

        // GUIStyle のキャッシュ
        private GUIStyle _summaryStyle;
        private GUIStyle _foldoutStyle;

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
            _results = CheckRunner.RunAll(_avatarRoot);
            CacheResultsByCategory();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("アバター改変おまもり", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            var newAvatarRoot = (GameObject)EditorGUILayout.ObjectField(
                "アバタールート", _avatarRoot, typeof(GameObject), true);
            if (newAvatarRoot != _avatarRoot)
            {
                _avatarRoot = newAvatarRoot;
                // アバターを指定した瞬間に自動チェックを走らせる。
                // オンボーディング時のボタン押し忘れを防ぎ、ツールの価値をすぐに体験してもらうため。
                if (_avatarRoot != null)
                {
                    _results = CheckRunner.RunAll(_avatarRoot);
                    CacheResultsByCategory();
                }
                else
                {
                    // アバター参照がクリアされたら結果も消す（古い結果が残ると誤解の元）
                    _results = null;
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_avatarRoot == null))
            {
                if (GUILayout.Button("チェック実行", GUILayout.Height(30)))
                {
                    _results = CheckRunner.RunAll(_avatarRoot);
                    CacheResultsByCategory();
                }
            }

            if (_results == null)
                return;

            EditorGUILayout.Space(4);

            // サマリー
            var summary = $"結果: {_errors.Count} Error / {_warnings.Count} Warning / {_infos.Count} Info";
            var summaryStyle = GetSummaryStyle();
            if (_errors.Count > 0)
                summaryStyle.normal.textColor = new Color(0.9f, 0.2f, 0.2f);
            else if (_warnings.Count > 0)
                summaryStyle.normal.textColor = new Color(0.9f, 0.7f, 0.1f);
            else
                summaryStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);

            EditorGUILayout.LabelField(summary, summaryStyle);
            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

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

            EditorGUILayout.EndScrollView();
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

            foldout = EditorGUILayout.Foldout(foldout, $"{label} ({items.Count})", true, style);
            if (!foldout)
                return;

            EditorGUI.indentLevel++;
            foreach (var result in items)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                // Severity アイコン
                var iconContent = GetSeverityIcon(result.Severity);
                if (iconContent != null)
                {
                    GUILayout.Label(iconContent, GUILayout.Width(20), GUILayout.Height(20));
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
