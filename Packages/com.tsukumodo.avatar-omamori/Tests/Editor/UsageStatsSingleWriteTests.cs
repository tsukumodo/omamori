using System.Collections.Generic;
using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 利用統計の書き込みを1往復に統合したこと（Issue #35）を固定するテスト。
    /// もともと入口を <see cref="UsageStatsRecorder.RecordCheckRun"/> と
    /// <see cref="UsageStatsRecorder.RecordDetections"/> の2つに分けていたのは check_run_count の
    /// 二重計上を避けるためだった。ここが崩れると、書き込み回数を1回に統合した意味そのものが失われる。
    /// </summary>
    public class UsageStatsSingleWriteTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = UsageStatsTestUtil.BeginOverride();
        }

        [TearDown]
        public void TearDown()
        {
            UsageStatsTestUtil.EndOverride(_dir);
        }

        [Test]
        public void RecordRun_チェック側とパフォーマンス側の検出を1回で渡すとcheck_run_countは1だけ増える()
        {
            var checkDetections = new Dictionary<string, int>
            {
                { "MissingScriptCheck", 2 },
            };
            var performanceCounts = new Dictionary<string, int>
            {
                { "performance_rank_pc", 1 },
                { "performance_rank_quest", 1 },
            };

            var merged = AvatarOmamoriWindow.MergeUsageCounts(checkDetections, performanceCounts);
            UsageStatsRecorder.RecordRun(merged, incrementRunCount: true);

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(1, stats.CheckRunCount);
            Assert.AreEqual(2, stats.DetectionCounts["MissingScriptCheck"]);
            Assert.AreEqual(1, stats.DetectionCounts["performance_rank_pc"]);
            Assert.AreEqual(1, stats.DetectionCounts["performance_rank_quest"]);
        }

        [Test]
        public void RecordRun_incrementRunCountがfalseならcheck_run_countは増えない()
        {
            UsageStatsRecorder.RecordRun(
                new Dictionary<string, int> { { "performance_rank_pc", 1 } },
                incrementRunCount: false);

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(0, stats.CheckRunCount);
            Assert.AreEqual(1, stats.DetectionCounts["performance_rank_pc"]);
        }

        [Test]
        public void 旧来のRecordCheckRunとRecordDetectionsを同じ実行で併用してもcheck_run_countは1のまま()
        {
            // 委譲後も、CheckRunner.RunAll と AvatarOmamoriWindow 側の記録を同じ実行で併用したときの
            // 従来の保証（実行回数を二重計上しない）が維持されていることを固定する
            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int> { { "MissingScriptCheck", 1 } });
            UsageStatsRecorder.RecordDetections(new Dictionary<string, int> { { "performance_rank_pc", 1 } });

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(1, stats.CheckRunCount);
            Assert.AreEqual(1, stats.DetectionCounts["MissingScriptCheck"]);
            Assert.AreEqual(1, stats.DetectionCounts["performance_rank_pc"]);
        }

        [Test]
        public void MergeUsageCounts_同じキーは合算されnull引数は空として扱われる()
        {
            var a = new Dictionary<string, int> { { "foo", 2 }, { "bar", 1 } };
            var b = new Dictionary<string, int> { { "foo", 3 } };

            var merged = AvatarOmamoriWindow.MergeUsageCounts(a, b);
            Assert.AreEqual(5, merged["foo"]);
            Assert.AreEqual(1, merged["bar"]);

            var mergedWithNullA = AvatarOmamoriWindow.MergeUsageCounts(null, b);
            Assert.AreEqual(3, mergedWithNullA["foo"]);

            var mergedWithNullB = AvatarOmamoriWindow.MergeUsageCounts(a, null);
            Assert.AreEqual(2, mergedWithNullB["foo"]);
            Assert.AreEqual(1, mergedWithNullB["bar"]);

            var mergedBothNull = AvatarOmamoriWindow.MergeUsageCounts(null, null);
            Assert.IsEmpty(mergedBothNull);
        }
    }
}
