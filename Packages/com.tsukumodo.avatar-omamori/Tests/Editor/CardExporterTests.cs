using AvatarOmamori.Editor;
using NUnit.Framework;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// カード画像のパフォーマンスランク行のテスト（v0.11.0 T-6 / DEC-094 決定4 / DEC-055）。
    ///
    /// <para>
    /// 描画そのもの（GL・動的フォントのアトラス）は EditorWindow の Repaint イベント中でしか
    /// 安定して動かないため、ここでは「行を出すか消すか」と「何を書くか」を決める純粋関数だけを検証する。
    /// 実際の見え方（y=436 の位置・色・グリフの崩れ）の確認は実機（T-8）で行う。
    /// </para>
    /// </summary>
    public class CardExporterTests
    {
        [Test]
        public void ランクが取れていなければカードのランク行は出ない()
        {
            // 「取得できませんでした」とも書かず、行ごと省略する（DEC-094 決定4）
            Assert.IsNull(CardExporter.BuildRankLine(null));
            Assert.IsNull(CardExporter.BuildRankLine(string.Empty));
        }

        [Test]
        public void ランクがあればラベル重さつきの1行になる()
        {
            var line = CardExporter.BuildRankLine("PC Poor ・ Quest Very Poor");

            Assert.AreEqual("重さ　PC Poor ・ Quest Very Poor", line);
        }
    }
}
