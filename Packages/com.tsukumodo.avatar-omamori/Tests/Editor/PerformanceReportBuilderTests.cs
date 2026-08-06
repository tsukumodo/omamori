using AvatarOmamori.Editor.Performance;
using NUnit.Framework;
using VRC.SDKBase.Validation.Performance;
using VRC.SDKBase.Validation.Performance.Stats;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 「あと何をどこまで減らすと1ランク上がるか」の算出を固定するテスト。
    ///
    /// <para>
    /// SDK の閾値は更新で変わりうるため、具体的な数値ではなく「満たすべき性質」を固定する。
    /// 数値を直接アサートすると SDK 更新のたびにテストが落ちるうえ、
    /// おまもり側は自前の閾値表を持たない方針（DEC-069 決定事項8）とも合わなくなる。
    /// </para>
    /// </summary>
    public class PerformanceReportBuilderTests
    {
        private static readonly PerformanceCategoryLabels.Entry PolyCountEntry =
            new PerformanceCategoryLabels.Entry(
                AvatarPerformanceCategory.PolyCount,
                "ポリゴン数",
                "polyCount",
                PerformanceCategoryLabels.ValueFormat.Count,
                PerformanceCategoryLabels.PerformanceDocUrl);

        [SetUp]
        public void SetUp()
        {
            // 閾値（LevelSet）が未読み込みだと GetStatLevelForRating が失敗する
            AvatarPerformanceStats.Initialize();
        }

        /// <summary>PC のランク閾値から polyCount の上限を取り出す。</summary>
        private static float PolyLimitFor(PerformanceRating rating)
        {
            var level = AvatarPerformanceStats.GetStatLevelForRating(rating, false);
            Assert.IsTrue(
                PerformanceCategoryLabels.TryGetNumericValue(level, "polyCount", out var limit),
                "SDK の閾値から polyCount を取得できませんでした");
            return limit;
        }

        [Test]
        public void ランクの順位はExcellentが0でVeryPoorが4()
        {
            Assert.AreEqual(0, PerformanceReportBuilder.RatingOrder(PerformanceRating.Excellent));
            Assert.AreEqual(4, PerformanceReportBuilder.RatingOrder(PerformanceRating.VeryPoor));
        }

        [Test]
        public void 判定対象外のランクは負の順位になる()
        {
            // None は「ランクが付かない」状態。要因の並べ替えに混ぜてはいけない
            Assert.Less(PerformanceReportBuilder.RatingOrder(PerformanceRating.None), 0);
        }

        [Test]
        public void 目標値は必ず現在値より小さい()
        {
            var current = PolyLimitFor(PerformanceRating.Poor) * 10f;

            var target = PerformanceReportBuilder.FindNextTarget(
                PolyCountEntry, current, PerformanceRating.VeryPoor, false);

            Assert.IsNotNull(target, "目標値が見つかりませんでした");
            Assert.Less(target.Value.Limit, current);
        }

        [Test]
        public void 目標ランクは現在の総合ランクより良い()
        {
            var current = PolyLimitFor(PerformanceRating.Poor) * 10f;

            var target = PerformanceReportBuilder.FindNextTarget(
                PolyCountEntry, current, PerformanceRating.VeryPoor, false);

            Assert.IsNotNull(target);
            Assert.Less(
                PerformanceReportBuilder.RatingOrder(target.Value.Rating),
                PerformanceReportBuilder.RatingOrder(PerformanceRating.VeryPoor));
        }

        [Test]
        public void 同値の閾値が並んでいても現在値より小さい段まで辿る()
        {
            // PC のポリゴン数は Good / Medium / Poor の閾値が同値のため、
            // 1つ上のランクをそのまま返すと「あと 0」になってしまう。
            // 現在値ちょうどを渡して、必ず「実際に減らす必要がある」値が返ることを固定する
            var current = PolyLimitFor(PerformanceRating.Poor);
            var strictest = PolyLimitFor(PerformanceRating.Excellent);

            var target = PerformanceReportBuilder.FindNextTarget(
                PolyCountEntry, current, PerformanceRating.VeryPoor, false);

            if (strictest < current)
            {
                // 現在値より小さい閾値が SDK 側に存在する以上、同値の段を飛ばして必ず見つかるはず
                Assert.IsNotNull(target, "現在値より小さい閾値があるのに目標値が返っていない");
                Assert.Less(target.Value.Limit, current, "現在値と同じ目標値を返してはいけない");
            }
            else
            {
                // すべての段が同値なら「これ以上減らす目標がない」が正しい
                Assert.IsNull(target);
            }
        }

        [Test]
        public void 十分に小さい値なら目標値は出ない()
        {
            var target = PerformanceReportBuilder.FindNextTarget(
                PolyCountEntry, 0f, PerformanceRating.VeryPoor, false);

            Assert.IsNull(target);
        }

        [Test]
        public void 総合ランクが最高なら目標値は出ない()
        {
            // Excellent の1つ上は存在しないので探索範囲が空になる
            var target = PerformanceReportBuilder.FindNextTarget(
                PolyCountEntry, 999999f, PerformanceRating.Excellent, false);

            Assert.IsNull(target);
        }

        [Test]
        public void 未知のフィールド名なら目標値は出ない()
        {
            // SDK 更新でフィールド名が変わっても例外にせず、その項目だけ静かに落とす
            var entry = new PerformanceCategoryLabels.Entry(
                AvatarPerformanceCategory.PolyCount,
                "ポリゴン数",
                "存在しないフィールド",
                PerformanceCategoryLabels.ValueFormat.Count,
                PerformanceCategoryLabels.PerformanceDocUrl);

            var target = PerformanceReportBuilder.FindNextTarget(
                entry, 999999f, PerformanceRating.VeryPoor, false);

            Assert.IsNull(target);
        }

        [Test]
        public void 超過率は現在値を目標値で割った値()
        {
            Assert.AreEqual(2f, PerformanceReportBuilder.ExcessRatio(100f, 50f));
        }

        [Test]
        public void 上限が0の項目は最優先で並ぶ()
        {
            // Quest のライト・Audio Source など「0 にするしかない」項目。
            // 0 除算にせず、必ず他のどの超過率よりも大きくなる必要がある
            Assert.AreEqual(float.MaxValue, PerformanceReportBuilder.ExcessRatio(3f, 0f));
            Assert.Greater(PerformanceReportBuilder.ExcessRatio(3f, 0f), PerformanceReportBuilder.ExcessRatio(1000f, 1f));
        }
    }
}
