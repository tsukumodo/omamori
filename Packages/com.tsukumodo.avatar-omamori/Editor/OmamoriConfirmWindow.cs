using System;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// おまもり用の共通「確認ウィンドウ」。
    /// <see cref="EditorWindow"/> ベースで画面中央に浮遊表示し、タイトルバー付きの独立ウィンドウとして見せる。
    /// 選択用の <see cref="DescriptorChoiceWindow"/> とスタイルを統一する。
    /// </summary>
    internal sealed class OmamoriConfirmWindow : EditorWindow
    {
        private string _message;
        private string _okLabel;
        private string _cancelLabel;
        private Action _onOk;

        public static void Show(
            string title,
            string message,
            string okLabel,
            string cancelLabel,
            Action onOk)
        {
            var window = CreateInstance<OmamoriConfirmWindow>();
            window.titleContent = new GUIContent(title);
            window._message = message;
            window._okLabel = okLabel;
            window._cancelLabel = cancelLabel;
            window._onOk = onOk;

            const float width = 480f;
            float height = CalcHeight(message);
            window.minSize = new Vector2(width, height);
            window.maxSize = new Vector2(width, height);
            window.position = OmamoriPopupStyles.CenterOfMainWindow(width, height);

            // ShowUtility: 常に手前に浮く浮遊ウィンドウ（タイトルバー付き、ドッキング不可）
            // EditorUtility.DisplayDialog に近い見た目で、他のウィンドウ操作もブロックしない
            window.ShowUtility();
        }

        private static float CalcHeight(string message)
        {
            float infoBoxHeight = OmamoriPopupStyles.CalcInfoBoxHeight(message);
            // 上下余白 + 情報ボックス + ボタン列 + 余白
            return 16f + infoBoxHeight + 12f + 30f + 16f;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            OmamoriPopupStyles.DrawInfoBox(_message);
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_cancelLabel, GUILayout.Height(26), GUILayout.Width(110)))
            {
                Close();
            }
            GUILayout.Space(8);
            if (GUILayout.Button(_okLabel, GUILayout.Height(26), GUILayout.Width(110)))
            {
                // 先にウィンドウを閉じてから FixAction を実行する（実行中にウィンドウが残らないように）
                var onOk = _onOk;
                Close();
                onOk?.Invoke();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }
    }
}
