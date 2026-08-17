using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// <see cref="CheckResult"/> の Hint / DocumentUrl フィールドを固定するテスト。
    /// 既存の10チェックはすべて省略した状態でコンストラクタを呼んでいるため、
    /// 省略時に両方 null になることを担保する（v0.10.0 T-2 で新設）。
    /// </summary>
    public class CheckResultTests
    {
        [Test]
        public void HintとDocumentUrlを渡すとそのまま保持される()
        {
            var result = new CheckResult(
                Severity.Warning,
                "何らかの問題",
                hint: "○○を△△に変更してください",
                documentUrl: "https://creators.vrchat.com/avatars/avatar-optimizing-tips/");

            Assert.AreEqual("○○を△△に変更してください", result.Hint);
            Assert.AreEqual("https://creators.vrchat.com/avatars/avatar-optimizing-tips/", result.DocumentUrl);
        }

        [Test]
        public void HintとDocumentUrlを省略すると両方nullになる()
        {
            // 既存の全チェッククラスがこの経路（未指定）でコンストラクタを呼んでいるため、
            // ここが崩れると全チェックの呼び出しに影響する
            var result = new CheckResult(Severity.Error, "何らかの問題");

            Assert.IsNull(result.Hint);
            Assert.IsNull(result.DocumentUrl);
        }
    }
}
