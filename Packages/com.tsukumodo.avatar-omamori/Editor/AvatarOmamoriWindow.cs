using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    public sealed class AvatarOmamoriWindow : EditorWindow
    {
        private GameObject _avatarRoot;
        private List<CheckResult> _results;
        private Vector2 _scrollPos;
        private bool _foldError = true;
        private bool _foldWarning = true;
        private bool _foldInfo = true;

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
                }
            }

            if (_results == null)
                return;

            EditorGUILayout.Space(4);

            var errors = _results.Where(r => r.Severity == Severity.Error).ToList();
            var warnings = _results.Where(r => r.Severity == Severity.Warning).ToList();
            var infos = _results.Where(r => r.Severity == Severity.Info).ToList();

            // サマリー
            var summary = $"結果: {errors.Count} Error / {warnings.Count} Warning / {infos.Count} Info";
            var summaryStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold
            };
            if (errors.Count > 0)
                summaryStyle.normal.textColor = new Color(0.9f, 0.2f, 0.2f);
            else if (warnings.Count > 0)
                summaryStyle.normal.textColor = new Color(0.9f, 0.7f, 0.1f);
            else
                summaryStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);

            EditorGUILayout.LabelField(summary, summaryStyle);
            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (errors.Count > 0)
                DrawSeverityGroup("Error", errors, ref _foldError, new Color(0.9f, 0.2f, 0.2f));
            if (warnings.Count > 0)
                DrawSeverityGroup("Warning", warnings, ref _foldWarning, new Color(0.9f, 0.7f, 0.1f));
            if (infos.Count > 0)
                DrawSeverityGroup("Info", infos, ref _foldInfo, new Color(0.5f, 0.7f, 1f));

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("問題は見つかりませんでした。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSeverityGroup(string label, List<CheckResult> items, ref bool foldout, Color color)
        {
            var style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
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
