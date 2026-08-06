using System.Linq;
using AvatarOmamori.Editor.Performance;
using NUnit.Framework;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// SDK の統計オブジェクトから値を取り出す経路のテスト。
    ///
    /// 実際の <c>AvatarPerformanceStats</c> は Nullable フィールドと入れ子（physBone）を持ち、
    /// 閾値側（<c>AvatarPerformanceStatsLevel</c>）は非 Nullable という違いがある。
    /// SDK のバージョン差でフィールドが消えても例外にせず「その項目だけ落ちる」ことも固定する。
    /// </summary>
    public class PerformanceCategoryLabelsTests
    {
        private sealed class NestedStub
        {
            public int componentCount = 12;
        }

        private sealed class StatsStub
        {
            public int? polyCount = 152955;
            public float? textureMegabytes = 176.5f;
            public int? missingValue = null;
            public bool? flag = true;
            public NestedStub physBone = new NestedStub();
            public NestedStub nullNested = null;
        }

        [Test]
        public void Nullableフィールドの値を取り出せる()
        {
            Assert.IsTrue(PerformanceCategoryLabels.TryGetNumericValue(new StatsStub(), "polyCount", out var value));
            Assert.AreEqual(152955f, value);
        }

        [Test]
        public void 入れ子のフィールドをドット区切りで取り出せる()
        {
            Assert.IsTrue(PerformanceCategoryLabels.TryGetNumericValue(new StatsStub(), "physBone.componentCount", out var value));
            Assert.AreEqual(12f, value);
        }

        [Test]
        public void nullのNullableは取得失敗になる()
        {
            Assert.IsFalse(PerformanceCategoryLabels.TryGetNumericValue(new StatsStub(), "missingValue", out _));
        }

        [Test]
        public void 入れ子がnullなら取得失敗になる()
        {
            Assert.IsFalse(PerformanceCategoryLabels.TryGetNumericValue(new StatsStub(), "nullNested.componentCount", out _));
        }

        [Test]
        public void 存在しないフィールドは例外にせず取得失敗にする()
        {
            Assert.IsFalse(PerformanceCategoryLabels.TryGetNumericValue(new StatsStub(), "notExistingField", out _));
        }

        [Test]
        public void 対象がnullでも例外にしない()
        {
            Assert.IsFalse(PerformanceCategoryLabels.TryGetNumericValue(null, "polyCount", out _));
        }

        [Test]
        public void 整数は3桁区切りで書式化する()
        {
            Assert.AreEqual("152,955", PerformanceCategoryLabels.FormatValue(152955f, PerformanceCategoryLabels.ValueFormat.Count));
        }

        [Test]
        public void メガバイトは単位つきで書式化する()
        {
            Assert.AreEqual("176.5 MB", PerformanceCategoryLabels.FormatValue(176.5078f, PerformanceCategoryLabels.ValueFormat.Megabytes));
        }

        [Test]
        public void bool型のフィールドは数値として取り出さない()
        {
            // Convert.ToSingle(bool) は例外にならず true→1f を返してしまう。
            // ParticleTrailsEnabled のような bool 項目を将来 Entries に足したときに
            // 内訳へ "1" と表示されてしまわないよう、bool は明示的に対象外にする
            Assert.IsFalse(PerformanceCategoryLabels.TryGetNumericValue(new StatsStub(), "flag", out var value));
            Assert.AreEqual(0f, value);
        }

        [Test]
        public void 内訳の定義にはビルド後にしか確定しない項目を含めない()
        {
            // DownloadSize / UncompressedSize はエディタ上では常に null になる（T-1 実機検証）。
            // 不定値を「あと何を減らす」に出さないよう、定義そのものから外れていることを固定する。
            foreach (var entry in PerformanceCategoryLabels.Entries)
            {
                StringAssert.DoesNotContain("downloadSize", entry.FieldPath);
                StringAssert.DoesNotContain("uncompressedSize", entry.FieldPath);
            }
        }

        [Test]
        public void 全項目にドキュメントURLが設定されている()
        {
            // Entry のコンストラクタは documentUrl を必須引数にしているが、
            // それでも「空文字を渡す」抜け道は残るため、実データ側でも空でないことを確認する
            foreach (var entry in PerformanceCategoryLabels.Entries)
            {
                Assert.IsFalse(
                    string.IsNullOrEmpty(entry.DocumentUrl),
                    $"{entry.Label} の DocumentUrl が未設定です");
            }
        }

        [Test]
        public void ドキュメントURLは全項目で同一ではない()
        {
            // 「26項目すべての『調べる』ボタンが同じURLを開く」という指摘の再発防止。
            // 項目ごとに専用ページへ振り分けたことを固定する（全部同じURLに戻ったら落ちる）。
            // しきい値は現在の実データ（12種類）よりだいぶ低い5にして、将来ページ構成が
            // 多少変わっても壊れすぎないようにしつつ、退行があれば確実に検知できるようにする
            var distinctUrlCount = PerformanceCategoryLabels.Entries
                .Select(entry => entry.DocumentUrl)
                .Distinct()
                .Count();

            Assert.GreaterOrEqual(distinctUrlCount, 5);
        }

        [Test]
        public void ドキュメントURLはすべてVRChat公式ドメインを指す()
        {
            foreach (var entry in PerformanceCategoryLabels.Entries)
            {
                StringAssert.StartsWith("https://creators.vrchat.com/", entry.DocumentUrl);
            }
        }
    }
}
