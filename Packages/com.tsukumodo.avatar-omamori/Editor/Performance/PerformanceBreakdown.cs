using System.Collections.Generic;
using UnityEngine;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// パーツ別パフォーマンス内訳の1件分。
    /// 「このパーツを消すと何が減るか」を表示するのに必要な情報だけを持つ。
    /// </summary>
    public sealed class PerformancePart
    {
        /// <summary>パーツの GameObject 名。</summary>
        public string Name { get; }

        /// <summary>ルートからの Hierarchy パス（<c>HierarchyPathUtil</c> で生成）。</summary>
        public string HierarchyPath { get; }

        /// <summary>対象の <see cref="UnityEngine.Renderer"/>。Hierarchy でのハイライト（選択）に使う。</summary>
        public Renderer Renderer { get; }

        /// <summary>
        /// このパーツを消すと減るテクスチャ使用量（MB・差分方式）。
        /// 他のパーツと絵柄（テクスチャ）を共有している場合、除いても総量が変わらないため 0 になりうる
        /// （単独帰属方式は採らない・v0.11.0 W0 設計 §2.2）。0 未満にはならない。
        /// </summary>
        public float TextureReductionMegabytes { get; }

        /// <summary>このパーツのポリゴン数。テクスチャと違い共有が起きないため、常に実数が積み上がる。</summary>
        public int PolyCount { get; }

        public PerformancePart(
            string name, string hierarchyPath, Renderer renderer, float textureReductionMegabytes, int polyCount)
        {
            Name = name;
            HierarchyPath = hierarchyPath;
            Renderer = renderer;
            TextureReductionMegabytes = textureReductionMegabytes;
            PolyCount = polyCount;
        }
    }

    /// <summary>
    /// パーツ別パフォーマンス内訳一式（不変）。
    ///
    /// <para>
    /// 対象はポリゴン数とテクスチャ使用量の2項目だけ（PhysBone の <c>transformCount</c> 等は
    /// per-component の SDK 関数が無いため対象外・DEC-094 決定3）。
    /// プラットフォーム（PC / Quest）では分けない。<c>AnalyzeMaterials</c> はビルドターゲットを
    /// 一切参照しないと IL 解析で確定済み（v0.11.0 W0 設計 §2.5）。
    /// </para>
    /// </summary>
    public sealed class PerformanceBreakdown
    {
        /// <summary>パーツ一覧。テクスチャ削減量の降順。</summary>
        public IReadOnlyList<PerformancePart> Parts { get; }

        /// <summary>全 Renderer で測ったテクスチャ使用量の総量（MB）。主画面の <c>AvatarPerformanceReport</c> と同じ経路の値。</summary>
        public float TotalTextureMegabytes { get; }

        /// <summary>全 Renderer のポリゴン数の合計。</summary>
        public int TotalPolyCount { get; }

        public PerformanceBreakdown(
            IReadOnlyList<PerformancePart> parts, float totalTextureMegabytes, int totalPolyCount)
        {
            Parts = parts ?? new List<PerformancePart>();
            TotalTextureMegabytes = totalTextureMegabytes;
            TotalPolyCount = totalPolyCount;
        }
    }
}
