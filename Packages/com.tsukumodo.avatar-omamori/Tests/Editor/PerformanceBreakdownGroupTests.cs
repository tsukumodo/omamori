using System.Collections.Generic;
using AvatarOmamori.Editor.Performance;
using NUnit.Framework;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 内訳をまとまり単位にしたあとの、値の書き分けと並びのテスト（DEC-109 / v0.11.0 W0 設計 §11.2）。
    ///
    /// <para>
    /// SDK 呼び出しを伴う実測値の一致（Komano_ClothesC が 64.5MB / 67,402）は実機で照合する。
    /// ここでは SDK に依存しない純粋関数だけを固定する。
    /// </para>
    /// </summary>
    public class PerformanceBreakdownGroupTests
    {
        [Test]
        public void テクスチャを持たないものは共有ではなくテクスチャなしと出す()
        {
            // 検証用キューブ（マテリアル null）を「共有しているため減りません」と書くのは嘘になる（DEC-109）
            Assert.AreEqual(
                TextureValueKind.NoTexture,
                PerformanceBreakdown.ClassifyTextureValue(reductionMegabytes: 0f, hasNoTexture: true));
        }

        [Test]
        public void テクスチャを持つのに減らないものは共有と出す()
        {
            Assert.AreEqual(
                TextureValueKind.Shared,
                PerformanceBreakdown.ClassifyTextureValue(reductionMegabytes: 0f, hasNoTexture: false));
        }

        [Test]
        public void 減る量があるときは数値として出す()
        {
            Assert.AreEqual(
                TextureValueKind.Amount,
                PerformanceBreakdown.ClassifyTextureValue(reductionMegabytes: 64.5f, hasNoTexture: false));
        }

        [Test]
        public void まとまりは外すと減るテクスチャの降順に並ぶ()
        {
            var groups = PerformanceBreakdownBuilder.OrderGroups(new List<PerformancePartGroup>
            {
                Group("Accessory", reduction: 1.2f, poly: 500),
                Group("ClothesC", reduction: 64.5f, poly: 67402),
            });

            Assert.AreEqual("ClothesC", groups[0].Name);
            Assert.AreEqual("Accessory", groups[1].Name);
        }

        [Test]
        public void 減る量が同点ならポリゴンの多い方が先に来る()
        {
            var groups = PerformanceBreakdownBuilder.OrderGroups(new List<PerformancePartGroup>
            {
                Group("Cube", reduction: 0f, poly: 12),
                Group("Gimmick", reduction: 0f, poly: 900),
            });

            Assert.AreEqual("Gimmick", groups[0].Name);
            Assert.AreEqual("Cube", groups[1].Name);
        }

        [Test]
        public void アバター本体は減る量が大きくても常に最後に来る()
        {
            // 本体は外す対象ではないので、並びの先頭を占めさせない（W0 設計 §11.2）
            var groups = PerformanceBreakdownBuilder.OrderGroups(new List<PerformancePartGroup>
            {
                Group("アバター本体", reduction: 999f, poly: 55821, isAvatarBody: true),
                Group("ClothesC", reduction: 64.5f, poly: 67402),
            });

            Assert.AreEqual("ClothesC", groups[0].Name);
            Assert.IsTrue(groups[1].IsAvatarBody);
        }

        private static PerformancePartGroup Group(
            string name, float reduction, int poly, bool isAvatarBody = false)
        {
            return new PerformancePartGroup(
                name, null, isAvatarBody, reduction, poly, false, new List<PerformancePart>());
        }
    }
}
