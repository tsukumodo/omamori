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

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("アバター改変おまもり", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _avatarRoot = (GameObject)EditorGUILayout.ObjectField(
                "アバタールート", _avatarRoot, typeof(GameObject), true);

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
                        var msg = result.FixConfirmMessage ?? "この問題を自動修正しますか？\nUndo（Ctrl+Z）で元に戻せます。";
                        if (EditorUtility.DisplayDialog("おまもり — 自動修正", msg, "修正する", "キャンセル"))
                        {
                            try
                            {
                                result.FixAction();
                                _results = CheckRunner.RunAll(_avatarRoot);
                                CacheResultsByCategory();
                                Repaint();
                            }
                            catch (Exception e)
                            {
                                EditorUtility.DisplayDialog("おまもり — エラー", $"修正中にエラーが発生しました。\n{e.Message}", "OK");
                            }
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUI.indentLevel--;
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
