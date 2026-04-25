using System;
using System.Linq;
using AvatarOmamori.Editor.Util;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// 「どの VRC Avatar Descriptor を残すか」を選ばせる確認ウィンドウ。
    /// <see cref="EditorWindow"/> ベースで画面中央に浮遊表示し、
    /// 確認用の <see cref="OmamoriConfirmWindow"/> とスタイルを統一する。
    /// </summary>
    internal sealed class DescriptorChoiceWindow : EditorWindow
    {
        private const string HelpMessage =
            "複数の VRC Avatar Descriptor が見つかりました。\n" +
            "残すものを選んでください。選んだもの以外は削除されます（Undo で戻せます）。";

        private GameObject _avatarRoot;
        private VRCAvatarDescriptor[] _ordered;
        private Action<VRCAvatarDescriptor> _onChosen;
        private Vector2 _scroll;

        public static void Show(
            GameObject avatarRoot,
            VRCAvatarDescriptor[] descriptors,
            Action<VRCAvatarDescriptor> onChosen)
        {
            var window = CreateInstance<DescriptorChoiceWindow>();
            window.titleContent = new GUIContent("おまもり — 残す Descriptor を選んでください");
            window._avatarRoot = avatarRoot;
            window._ordered = descriptors
                .OrderBy(d => d.gameObject == avatarRoot ? 0 : 1)
                .ThenBy(d => HierarchyPathUtil.GetHierarchyPath(d.gameObject))
                .ToArray();
            window._onChosen = onChosen;

            const float width = 520f;
            float height = CalcHeight(window._ordered.Length);
            window.minSize = new Vector2(width, height);
            window.maxSize = new Vector2(width, height);
            window.position = OmamoriPopupStyles.CenterOfMainWindow(width, height);

            window.ShowUtility();
        }

        private static float CalcHeight(int itemCount)
        {
            const float perItem = 30f;      // 候補ボタン1行
            const float footerHeight = 40f; // キャンセル + 余白
            const float paddingHeight = 24f;
            const float maxHeight = 600f;

            float infoBoxHeight = OmamoriPopupStyles.CalcInfoBoxHeight(HelpMessage);
            float height = paddingHeight + infoBoxHeight + itemCount * perItem + footerHeight;
            return Mathf.Min(height, maxHeight);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            OmamoriPopupStyles.DrawInfoBox(HelpMessage);
            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var descriptor in _ordered)
            {
                if (descriptor == null) continue;
                var go = descriptor.gameObject;
                bool isRoot = go == _avatarRoot;
                string label = isRoot
                    ? $"本体「{go.name}」を残す（推奨）"
                    : $"子「{HierarchyPathUtil.GetHierarchyPath(go)}」を残す";

                if (GUILayout.Button(label, GUILayout.Height(26)))
                {
                    var keep = descriptor;
                    var callback = _onChosen;
                    Close();
                    callback?.Invoke(keep);
                    return;
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("キャンセル", GUILayout.Height(24), GUILayout.Width(110)))
            {
                Close();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }
    }
}
