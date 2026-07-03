using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    public class UsageStatsTests
    {
        [Test]
        public void Clone_ディープコピーなので元への変更がコピーに影響しない()
        {
            var original = new UsageStats { CheckRunCount = 1 };
            original.DetectionCounts["MissingScriptCheck"] = 2;

            var clone = original.Clone();
            original.DetectionCounts["MissingScriptCheck"] = 99;
            original.FixRunCounts["AnimatorLayerWeightCheck"] = 1;

            Assert.AreEqual(1, clone.CheckRunCount);
            Assert.AreEqual(2, clone.DetectionCounts["MissingScriptCheck"]);
            Assert.IsFalse(clone.FixRunCounts.ContainsKey("AnimatorLayerWeightCheck"));
        }
    }
}
