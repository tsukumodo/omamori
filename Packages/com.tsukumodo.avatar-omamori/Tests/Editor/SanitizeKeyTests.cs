using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// SanitizeKey は「個人情報（アバター名・パス・日本語など）をキーとして保存しない」という
    /// README / VPM LP でのユーザーへの約束（DEC-055）を支える最終防波堤。
    /// この保証が将来の変更で緩まないことをテストで固定する。
    /// </summary>
    public class SanitizeKeyTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void 空のキーはnullになる(string raw)
        {
            Assert.IsNull(UsageStatsRecorder.SanitizeKey(raw));
        }

        [TestCase("MissingScriptCheck")]
        [TestCase("_under_score_09")]
        [TestCase("ABCxyz012")]
        public void ASCII識別子はそのまま通る(string raw)
        {
            Assert.AreEqual(raw, UsageStatsRecorder.SanitizeKey(raw));
        }

        [Test]
        public void 上限64文字ちょうどは通る()
        {
            var key = new string('a', 64);

            Assert.AreEqual(key, UsageStatsRecorder.SanitizeKey(key));
        }

        [Test]
        public void 上限を超える65文字はnullになる()
        {
            Assert.IsNull(UsageStatsRecorder.SanitizeKey(new string('a', 65)));
        }

        [TestCase("アバター名")]
        [TestCase("Assets/Foo.prefab")]
        [TestCase("has space")]
        [TestCase("has-hyphen")]
        [TestCase("emoji😀mixed")]
        public void 日本語やパスや記号を含むキーはnullになる(string raw)
        {
            Assert.IsNull(UsageStatsRecorder.SanitizeKey(raw));
        }
    }
}
