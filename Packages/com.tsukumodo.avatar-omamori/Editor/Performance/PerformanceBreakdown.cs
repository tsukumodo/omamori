using System.Collections.Generic;
using UnityEngine;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// 「外すと減る量」の欄に何を出すか。数値・共有・テクスチャなしの3通り（v0.11.0 W0 設計 §11.2）。
    /// </summary>
    public enum TextureValueKind
    {
        /// <summary>実際に減る量がある。数値を出す。</summary>
        Amount,

        /// <summary>テクスチャは持っているが、他と絵柄を使い回しているため単独では減らない。</summary>
        Shared,

        /// <summary>そもそもテクスチャを持っていない（マテリアル無し・単色など）。</summary>
        NoTexture,
    }

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

        /// <summary>
        /// このパーツ単独では 0MB か（＝テクスチャを1枚も持たない）。
        /// 判定は「そのパーツだけを SDK に測らせて 0MB かどうか」で、自前計算はしない（DEC-069 決定8）。
        /// 0 を「共有しているため」と書くと、テクスチャを持たない検証用キューブに嘘をつくことになる（DEC-109）。
        /// </summary>
        public bool HasNoTexture { get; }

        public PerformancePart(
            string name, string hierarchyPath, Renderer renderer,
            float textureReductionMegabytes, int polyCount, bool hasNoTexture)
        {
            Name = name;
            HierarchyPath = hierarchyPath;
            Renderer = renderer;
            TextureReductionMegabytes = textureReductionMegabytes;
            PolyCount = polyCount;
            HasNoTexture = hasNoTexture;
        }

        /// <summary>このパーツの「外すと減るテクスチャ」欄に出す種別。</summary>
        public TextureValueKind TextureValue
        {
            get { return PerformanceBreakdown.ClassifyTextureValue(TextureReductionMegabytes, HasNoTexture); }
        }
    }

    /// <summary>
    /// 内訳のまとまり1件（後から足した服・装飾品ひとかたまり、またはアバター本体）。
    /// まとまりの決め方は <see cref="AvatarPartGrouping"/>（v0.11.0 W0 設計 §11.3・DEC-109）。
    /// </summary>
    public sealed class PerformancePartGroup
    {
        /// <summary>見出しに出す名前。</summary>
        public string Name { get; }

        /// <summary>まとまりの一番外側の GameObject。アバター本体は null。</summary>
        public GameObject Root { get; }

        /// <summary>アバター本体（外す対象ではない）かどうか。並びでは常に最後。</summary>
        public bool IsAvatarBody { get; }

        /// <summary>
        /// このまとまりを丸ごと外すと減るテクスチャ使用量（MB）。
        /// まとまりの中で絵柄を使い回していても、まとまりごと外せば減るので、
        /// パーツ単位の値の合計とは一致しない（そこが DEC-109 で作り直した理由）。
        /// </summary>
        public float TextureReductionMegabytes { get; }

        /// <summary>このまとまりのポリゴン数の合計。</summary>
        public int PolyCount { get; }

        /// <summary>まとまり全体でテクスチャを1枚も持たないか。</summary>
        public bool HasNoTexture { get; }

        /// <summary>中のパーツ。ポリゴン数の降順。</summary>
        public IReadOnlyList<PerformancePart> Parts { get; }

        public PerformancePartGroup(
            string name, GameObject root, bool isAvatarBody,
            float textureReductionMegabytes, int polyCount, bool hasNoTexture,
            IReadOnlyList<PerformancePart> parts)
        {
            Name = name;
            Root = root;
            IsAvatarBody = isAvatarBody;
            TextureReductionMegabytes = textureReductionMegabytes;
            PolyCount = polyCount;
            HasNoTexture = hasNoTexture;
            Parts = parts ?? new List<PerformancePart>();
        }

        /// <summary>このまとまりの「外すと減るテクスチャ」欄に出す種別。</summary>
        public TextureValueKind TextureValue
        {
            get { return PerformanceBreakdown.ClassifyTextureValue(TextureReductionMegabytes, HasNoTexture); }
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
        /// <summary>
        /// まとまり一覧（服・装飾品ごと）。「外すと減るテクスチャ」降順・同点はポリゴン降順、アバター本体は常に最後。
        /// v0.11.0 の表示はこちらを使う（DEC-109）。
        /// </summary>
        public IReadOnlyList<PerformancePartGroup> Groups { get; }

        /// <summary>
        /// パーツ一覧（まとまりをまたいだ平坦なリスト）。テクスチャ削減量の降順。
        /// R-3 でウィンドウをまとまり単位に作り直すまでの経過措置。
        /// </summary>
        public IReadOnlyList<PerformancePart> Parts { get; }

        /// <summary>全 Renderer で測ったテクスチャ使用量の総量（MB）。主画面の <c>AvatarPerformanceReport</c> と同じ経路の値。</summary>
        public float TotalTextureMegabytes { get; }

        /// <summary>全 Renderer のポリゴン数の合計。</summary>
        public int TotalPolyCount { get; }

        public PerformanceBreakdown(
            IReadOnlyList<PerformancePartGroup> groups,
            IReadOnlyList<PerformancePart> parts,
            float totalTextureMegabytes,
            int totalPolyCount)
        {
            Groups = groups ?? new List<PerformancePartGroup>();
            Parts = parts ?? new List<PerformancePart>();
            TotalTextureMegabytes = totalTextureMegabytes;
            TotalPolyCount = totalPolyCount;
        }

        /// <summary>
        /// 「外すと減るテクスチャ」欄の種別を決める純粋関数。
        /// テクスチャを持たないものを「共有しているため減りません」と書かないためにここで分ける（DEC-109）。
        /// </summary>
        public static TextureValueKind ClassifyTextureValue(float reductionMegabytes, bool hasNoTexture)
        {
            if (hasNoTexture) return TextureValueKind.NoTexture;
            return reductionMegabytes > 0f ? TextureValueKind.Amount : TextureValueKind.Shared;
        }
    }
}
