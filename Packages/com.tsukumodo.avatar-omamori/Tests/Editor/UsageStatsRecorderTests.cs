using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    public class UsageStatsRecorderTests
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
        public void RecordCheckRun_実行回数と種別ごとの検出件数が加算される()
        {
            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int>
            {
                { "MissingScriptCheck", 3 },
                { "DescriptorDuplicateCheck", 1 },
            });
            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int>
            {
                { "MissingScriptCheck", 2 },
            });

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(2, stats.CheckRunCount);
            Assert.AreEqual(5, stats.DetectionCounts["MissingScriptCheck"]);
            Assert.AreEqual(1, stats.DetectionCounts["DescriptorDuplicateCheck"]);
        }

        [Test]
        public void RecordCheckRun_0件以下のエントリと不正キーは記録されない()
        {
            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int>
            {
                { "ZeroCheck", 0 },
                { "NegativeCheck", -1 },
                { "アバター名", 5 },
            });

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(1, stats.CheckRunCount);
            Assert.IsEmpty(stats.DetectionCounts);
        }

        [Test]
        public void RecordFix_種別ごとの修正回数が加算される()
        {
            UsageStatsRecorder.RecordFix("AnimatorLayerWeightCheck");
            UsageStatsRecorder.RecordFix("AnimatorLayerWeightCheck");

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(2, stats.FixRunCounts["AnimatorLayerWeightCheck"]);
        }

        [Test]
        public void OptOut中は何も記録されない()
        {
            UsageStatsRecorder.SetOptOut(true);

            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int> { { "MissingScriptCheck", 1 } });
            UsageStatsRecorder.RecordFix("MissingScriptCheck");

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.IsTrue(stats.OptOut);
            Assert.AreEqual(0, stats.CheckRunCount);
            Assert.IsEmpty(stats.DetectionCounts);
            Assert.IsEmpty(stats.FixRunCounts);
        }

        [Test]
        public void 記録される日付は年月日のみの形式になる()
        {
            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int>());

            var stats = UsageStatsRecorder.GetSnapshot();
            var dateOnly = new Regex(@"^\d{4}-\d{2}-\d{2}$");
            Assert.IsTrue(dateOnly.IsMatch(stats.FirstRun), $"FirstRun が年月日のみの形式でない: {stats.FirstRun}");
            Assert.IsTrue(dateOnly.IsMatch(stats.LastRun), $"LastRun が年月日のみの形式でない: {stats.LastRun}");
        }

        [Test]
        public void 保存済みファイルの不正キーは読み込み時に捨てられる()
        {
            var json = "{\n" +
                       "  \"schema_version\": 1,\n" +
                       "  \"tool_version\": \"0.6.0\",\n" +
                       "  \"first_run\": \"2026-07-01\",\n" +
                       "  \"last_run\": \"2026-07-01\",\n" +
                       "  \"opt_out\": false,\n" +
                       "  \"check_run_count\": 3,\n" +
                       "  \"detection_counts\": [\n" +
                       "    { \"key\": \"アバター名\", \"count\": 5 },\n" +
                       "    { \"key\": \"MissingScriptCheck\", \"count\": 2 }\n" +
                       "  ],\n" +
                       "  \"fix_run_counts\": [],\n" +
                       "  \"last_exported_at\": \"\"\n" +
                       "}\n";
            File.WriteAllText(Path.Combine(_dir, "usage-stats.json"), json);
            UsageStatsRecorder.ResetForTests(); // キャッシュを捨てて上のファイルを読み直させる

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(3, stats.CheckRunCount);
            Assert.IsFalse(stats.DetectionCounts.ContainsKey("アバター名"), "不正キーが読み込み時に除去されていない");
            Assert.AreEqual(2, stats.DetectionCounts["MissingScriptCheck"]);
        }

        [Test]
        public void ClearStats_カウントは消えるがOptOut設定は保持される()
        {
            UsageStatsRecorder.SetOptOut(true);

            UsageStatsRecorder.ClearStats();

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.IsTrue(stats.OptOut);
            Assert.AreEqual(0, stats.CheckRunCount);
            Assert.IsEmpty(stats.DetectionCounts);
        }

        [Test]
        public void 記録はディスクに永続化されキャッシュ破棄後も読める()
        {
            UsageStatsRecorder.RecordCheckRun(new Dictionary<string, int> { { "MissingScriptCheck", 1 } });

            UsageStatsRecorder.ResetForTests();

            var stats = UsageStatsRecorder.GetSnapshot();
            Assert.AreEqual(1, stats.CheckRunCount);
            Assert.AreEqual(1, stats.DetectionCounts["MissingScriptCheck"]);
        }
    }
}
