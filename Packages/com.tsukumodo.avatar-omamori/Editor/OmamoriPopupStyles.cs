using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// おまもりのポップアップで共通利用する GUIStyle と描画ヘルパー。
    /// <see cref="EditorStyles.helpBox"/> のデフォルトフォントサイズは小さすぎて読みにくいため、
    /// 12pt 相当に拡大したスタイルを本文表示に使う。
    /// </summary>
    internal static class OmamoriPopupStyles
    {
        private const float LineHeight = 18f;
        private const float BoxVerticalPadding = 16f;

        private static GUIStyle _infoBoxStyle;

        /// <summary>本文用の拡大 HelpBox スタイル（fontSize 12 / 折り返しあり）。</summary>
        public static GUIStyle InfoBox
        {
            get
            {
                // GUIStyle は OnGUI 内でしか初期化できないため遅延生成する
                if (_infoBoxStyle == null)
                {
                    _infoBoxStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        fontSize = 12,
                        wordWrap = true,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(10, 10, 8, 8),
                    };
                }
                return _infoBoxStyle;
            }
        }

        /// <summary>
        /// HelpBox 相当の見た目（情報アイコン + 枠）で、拡大フォントの説明文を描画する。
        /// </summary>
        public static void DrawInfoBox(string message)
        {
            var icon = EditorGUIUtility.IconContent("console.infoicon");
            var content = new GUIContent(" " + message, icon.image);
            GUILayout.Label(content, InfoBox);
        }

        /// <summary>
        /// <see cref="DrawInfoBox"/> で描画した場合の想定高さ（概算）。
        /// 明示的な改行数に応じて計算する（折り返しは考慮しないが、ウィンドウ幅が十分広ければ問題ない）。
        /// </summary>
        public static float CalcInfoBoxHeight(string message)
        {
            int lineCount = Mathf.Max(1, CountLines(message));
            return BoxVerticalPadding + lineCount * LineHeight;
        }

        /// <summary>
        /// Unity メインエディタウィンドウの中央に指定サイズの Rect を配置する。
        /// 確認・選択ウィンドウの <see cref="EditorWindow.position"/> 計算で使う。
        /// </summary>
        public static Rect CenterOfMainWindow(float width, float height)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(
                main.x + (main.width - width) / 2f,
                main.y + (main.height - height) / 2f,
                width,
                height);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int n = 1;
            foreach (var c in text)
            {
                if (c == '\n') n++;
            }
            return n;
        }
    }
}
