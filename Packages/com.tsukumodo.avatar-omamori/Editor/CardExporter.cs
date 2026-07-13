using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// チェック結果のセッションサマリーを SNS 共有用のカード画像（PNG）として書き出す静的ユーティリティ。
    ///
    /// <para>
    /// 【描画方式（v0.7.0 PoC 方式C・実証済み）】
    /// オフスクリーンの <see cref="RenderTexture"/> へ、動的フォントのグリフアトラス
    /// （<see cref="Font.RequestCharactersInTexture"/>）を使い <see cref="GL"/>.QUADS で直描画する。
    /// IMGUI（GUIStyle.Draw）は EditorWindow の GUI クリップ矩形の外を描画しないため、
    /// カード座標（1200x630）のテキストがクリップされる。GL 直描画はクリップ非依存で回避できる。
    /// 日本語・異体字は OS フォントフォールバックで同アトラスに焼かれる。
    /// </para>
    /// <para>
    /// 【色管理（実装ノート参照）】
    /// Linear カラースペース（VRChat プロジェクトの既定）で sRGB RT に書き込むと色が二重変換され
    /// 白浮きするため、<see cref="RenderTextureReadWrite.Linear"/>（変換なし）＋ <c>GL.sRGBWrite=false</c> で
    /// 色管理をバイパスし、指定した色のバイト値をそのまま PNG に通す。
    /// </para>
    /// <para>
    /// 【呼び出しの制約】GL 描画とフォントアトラスの状態が安定している EditorWindow の
    /// Repaint イベント中に呼ぶこと。ボタン押下イベント中に直接呼ぶと描画が乱れる。
    /// </para>
    /// <para>
    /// 【個人情報境界（DEC-055）】カードにはアバター名・GUID・シーン名・パスを一切載せない。
    /// 受け取るのは件数・日付（年月日のみ）・ツールバージョンなど、個人を特定しえない集計値のみ。
    /// </para>
    /// </summary>
    public static class CardExporter
    {
        private const int CardWidth = 1200;   // OGP 比 1200x630
        private const int CardHeight = 630;
        private const int RenderScale = 2;    // 内部は 2x で描画してから縮小し、文字エッジを滑らかにする

        // おまもり個別カラー（抹茶トーン）
        private static readonly Color BgColor = new Color32(0x46, 0x7A, 0x56, 0xFF);
        private static readonly Color BandColor = new Color32(0x3A, 0x68, 0x49, 0xFF);   // 下帯（背景より一段暗い）
        private static readonly Color TextColor = new Color32(0xF0, 0xE8, 0xD0, 0xFF);
        private static readonly Color SubTextColor = new Color32(0xD4, 0xCC, 0xB2, 0xFF);
        private static readonly Color GoldColor = new Color32(0xC8, 0xA8, 0x7C, 0xFF);   // 全クリアの「✓」に使う金色

        /// <summary>カードに載せるセッションサマリーの集計値（個人情報は含めない・DEC-055）。</summary>
        public struct CardData
        {
            /// <summary>検出したエラー件数。</summary>
            public int ErrorCount;
            /// <summary>検出した警告件数。</summary>
            public int WarningCount;
            /// <summary>検出した情報件数。</summary>
            public int InfoCount;
            /// <summary>このセッションでの自動修正の実行件数。</summary>
            public int FixCount;
            /// <summary>日付文字列（"yyyy-MM-dd"・年月日のみ）。</summary>
            public string DateText;
            /// <summary>ツールバージョン（例: "0.7.0"）。空なら表記を省く。</summary>
            public string ToolVersion;

            /// <summary>エラー・警告・情報がすべて 0 件（＝全クリア）か。</summary>
            public bool IsAllClear => ErrorCount == 0 && WarningCount == 0 && InfoCount == 0;
        }

        /// <summary>
        /// カードを描画して <paramref name="path"/> に PNG として書き出す。
        /// 例外は呼び出し側で扱えるようにそのまま送出する（呼び出し側で握って UI に通知する）。
        /// GL 描画を伴うため EditorWindow の Repaint イベント中に呼ぶこと。
        /// </summary>
        public static void ExportPng(string path, CardData data)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("出力パスが空です。", nameof(path));

            int w = CardWidth * RenderScale;
            int h = CardHeight * RenderScale;

            // 色変換を完全に無効化する（Linear 環境で sRGB RT に書くと二重変換で白浮きするため）。
            var rw = RenderTextureReadWrite.Linear;

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, rw);
            var prevActive = RenderTexture.active;
            bool prevSrgbWrite = GL.sRGBWrite;

            Texture2D tex = null;
            try
            {
                Font font = ResolveFont();

                RenderTexture.active = rt;
                GL.sRGBWrite = false;

                GL.PushMatrix();
                GL.LoadPixelMatrix(0, w, h, 0); // 左上原点・ピクセル座標系
                GL.Clear(true, true, BgColor);

                DrawCard(w, h, RenderScale, font, data);

                GL.PopMatrix();

                // 2x で描いたものをバイリニア縮小して出力解像度に落とす
                var small = RenderTexture.GetTemporary(CardWidth, CardHeight, 0, RenderTextureFormat.ARGB32, rw);
                try
                {
                    Graphics.Blit(rt, small);
                    RenderTexture.active = small;
                    tex = ReadBack(CardWidth, CardHeight);
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(small);
                }

                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prevActive;
                GL.sRGBWrite = prevSrgbWrite;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>カード1枚分の内容を現在アクティブな RT に描画する（座標はカード座標 × scale）。</summary>
        private static void DrawCard(int w, int h, int scale, Font font, CardData data)
        {
            const string headerText = "アバター改変おまもり ─ チェック結果";
            const string checkMark = "✓ ";
            string mainMessage = data.IsAllClear ? "問題は見つかりませんでした" : "チェックが完了しました";
            string countsText = $"エラー {data.ErrorCount} ・ 警告 {data.WarningCount} ・ 情報 {data.InfoCount}";
            string fixText = $"自動修正 {data.FixCount}件";
            string dateText = data.DateText ?? "";
            string wordmarkText = string.IsNullOrEmpty(data.ToolVersion)
                ? "つくも堂 TSUKUMODO"
                : $"つくも堂 TSUKUMODO ・ v{data.ToolVersion}";

            const int headerSize = 24;
            const int mainSize = 52;
            const int countsSize = 32;
            const int fixSize = 26;
            const int footSize = 22;

            // 動的フォントのグリフを全テキスト×全サイズ分、描画前にまとめてベイクする
            // （描画途中のアトラス再構築で取得済み UV が無効になるのを防ぐ）
            RequestGlyphs(font, headerText, headerSize * scale, FontStyle.Normal);
            if (data.IsAllClear) RequestGlyphs(font, checkMark, mainSize * scale, FontStyle.Bold);
            RequestGlyphs(font, mainMessage, mainSize * scale, FontStyle.Bold);
            RequestGlyphs(font, countsText, countsSize * scale, FontStyle.Normal);
            RequestGlyphs(font, fixText, fixSize * scale, FontStyle.Normal);
            RequestGlyphs(font, dateText, footSize * scale, FontStyle.Normal);
            RequestGlyphs(font, wordmarkText, footSize * scale, FontStyle.Bold);

            // 下帯（ブランド帯）
            FillRect(new Rect(0, (CardHeight - 78) * scale, w, 78 * scale), BandColor);

            // ヘッダー（左上）
            DrawTextGL(font, headerText, headerSize * scale, FontStyle.Normal, SubTextColor,
                R(60, 44, 1080, 40, scale), TextAnchor.UpperLeft);

            // メイン文言（中央・大）。全クリア時のみ頭に金色の「✓」を添える（案B・DEC）
            var mainRect = R(60, 205, 1080, 90, scale);
            if (data.IsAllClear)
            {
                DrawTwoColorCentered(font, checkMark, GoldColor, mainMessage, TextColor,
                    mainSize * scale, FontStyle.Bold, mainRect);
            }
            else
            {
                DrawTextGL(font, mainMessage, mainSize * scale, FontStyle.Bold, TextColor,
                    mainRect, TextAnchor.MiddleCenter);
            }

            // 件数
            DrawTextGL(font, countsText, countsSize * scale, FontStyle.Normal, TextColor,
                R(60, 322, 1080, 48, scale), TextAnchor.MiddleCenter);

            // 自動修正件数
            DrawTextGL(font, fixText, fixSize * scale, FontStyle.Normal, SubTextColor,
                R(60, 388, 1080, 40, scale), TextAnchor.MiddleCenter);

            // 下帯: 左に日付、右にワードマーク
            // ※ ワードマークは当面テキスト描画。PNG 素材が用意できたらこの2行を差し替える（T-4 の口）。
            DrawTextGL(font, dateText, footSize * scale, FontStyle.Normal, SubTextColor,
                R(60, 576, 500, 32, scale), TextAnchor.MiddleLeft);
            DrawTextGL(font, wordmarkText, footSize * scale, FontStyle.Bold, TextColor,
                R(640, 576, 500, 32, scale), TextAnchor.MiddleRight);
        }

        /// <summary>カード座標（1200x630 基準）の Rect を scale 倍した実ピクセル Rect に変換する。</summary>
        private static Rect R(float x, float y, float width, float height, int scale)
        {
            return new Rect(x * scale, y * scale, width * scale, height * scale);
        }

        /// <summary>
        /// 描画に使うフォントを解決する。GUI.skin / EditorStyles の既定フォントを使い、
        /// 日本語・異体字は OS フォントフォールバックで同じアトラスに焼かれる。
        /// GUI.skin のフォントは破棄禁止（Unity 所有）のため、ここでは新規生成しない。
        /// </summary>
        private static Font ResolveFont()
        {
            if (GUI.skin != null && GUI.skin.font != null) return GUI.skin.font;
            return EditorStyles.label.font;
        }

        private static void RequestGlyphs(Font font, string text, int size, FontStyle style)
        {
            if (font == null || string.IsNullOrEmpty(text)) return;
            font.RequestCharactersInTexture(text, size, style);
        }

        /// <summary>
        /// 2色のテキストを横に連結して <paramref name="rect"/> 内で水平中央に描く。
        /// 全クリア文言の頭に金色の「✓」を添えるために使う。
        /// </summary>
        private static void DrawTwoColorCentered(
            Font font, string first, Color firstColor, string second, Color secondColor,
            int size, FontStyle style, Rect rect)
        {
            if (font == null) return;
            float w1 = MeasureWidth(font, first, size, style);
            float w2 = MeasureWidth(font, second, size, style);
            float startX = rect.x + (rect.width - (w1 + w2)) / 2f;
            float baseline = rect.y + rect.height / 2f + size * 0.35f;

            float pen = DrawTextAt(font, first, size, style, firstColor, startX, baseline);
            DrawTextAt(font, second, size, style, secondColor, pen, baseline);
        }

        /// <summary>
        /// 動的フォントのグリフアトラスを使い、GL.QUADS でテキストを直描画する（GUI クリップ非依存）。
        /// アトラスに無い文字（絵文字等）はスキップする。
        /// </summary>
        private static void DrawTextGL(Font font, string text, int size, FontStyle style, Color color, Rect rect, TextAnchor anchor)
        {
            if (font == null || string.IsNullOrEmpty(text)) return;

            float width = MeasureWidth(font, text, size, style);
            float x;
            if (anchor == TextAnchor.MiddleCenter) x = rect.x + (rect.width - width) / 2f;
            else if (anchor == TextAnchor.MiddleRight) x = rect.xMax - width;
            else x = rect.x;

            // ベースラインの近似: 中段アンカーは矩形中央 + 0.35em、上段アンカーは上端 + 0.9em
            float baseline = anchor == TextAnchor.UpperLeft
                ? rect.y + size * 0.9f
                : rect.y + rect.height / 2f + size * 0.35f;

            DrawTextAt(font, text, size, style, color, x, baseline);
        }

        /// <summary>
        /// テキストを開始ペン位置 <paramref name="penX"/>・ベースライン <paramref name="baseline"/> から
        /// 左詰めで直描画し、描き終えたペン位置（次の文字の開始 x）を返す。
        /// </summary>
        private static float DrawTextAt(Font font, string text, int size, FontStyle style, Color color, float penX, float baseline)
        {
            if (font == null || string.IsNullOrEmpty(text)) return penX;

            font.material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(color);

            float pen = penX;
            foreach (char c in text)
            {
                if (!font.GetCharacterInfo(c, out var ci, size, style))
                    continue; // アトラスに無い文字（絵文字等）はスキップ
                float x0 = pen + ci.minX;
                float x1 = pen + ci.maxX;
                float y0 = baseline - ci.maxY; // 上（y下向き座標系）
                float y1 = baseline - ci.minY; // 下

                GL.TexCoord2(ci.uvTopLeft.x, ci.uvTopLeft.y);
                GL.Vertex3(x0, y0, 0);
                GL.TexCoord2(ci.uvTopRight.x, ci.uvTopRight.y);
                GL.Vertex3(x1, y0, 0);
                GL.TexCoord2(ci.uvBottomRight.x, ci.uvBottomRight.y);
                GL.Vertex3(x1, y1, 0);
                GL.TexCoord2(ci.uvBottomLeft.x, ci.uvBottomLeft.y);
                GL.Vertex3(x0, y1, 0);

                pen += ci.advance;
            }

            GL.End();
            return pen;
        }

        private static float MeasureWidth(Font font, string text, int size, FontStyle style)
        {
            if (font == null || string.IsNullOrEmpty(text)) return 0f;
            float w = 0f;
            foreach (char c in text)
            {
                if (font.GetCharacterInfo(c, out var ci, size, style)) w += ci.advance;
            }
            return w;
        }

        private static void FillRect(Rect rect, Color color)
        {
            // Graphics.DrawTexture の color はニュートラルが (0.5,0.5,0.5,0.5) で2倍乗算されるため 1/2 を渡す
            Graphics.DrawTexture(rect, Texture2D.whiteTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, color * 0.5f);
        }

        private static Texture2D ReadBack(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            return tex;
        }
    }
}
