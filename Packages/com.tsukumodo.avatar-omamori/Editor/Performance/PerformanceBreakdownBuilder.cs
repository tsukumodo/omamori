using System.Collections.Generic;
using System.Linq;
using AvatarOmamori.Editor.Util;
using UnityEngine;
using VRC.SDKBase.Validation.Performance.Stats;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// 内訳（差分方式）を「服・装飾品のまとまり」単位で組み立てる（DEC-109 / v0.11.0 W0 設計 §11）。
    ///
    /// <para>
    /// 単独帰属（そのパーツだけを測る）は採らない。共有テクスチャが多重計上され、
    /// 合計が実際の数倍に水増しされるため（8/8 実測: 850.3MB vs 実際 336.8MB・W0 設計 §2.2）。
    /// 「全 Renderer で測った総量」から「その対象を除いた集合で測り直した量」を引いた差分を削減量として扱う。
    /// </para>
    /// <para>
    /// パーツ単位だけだと、衣装の中で絵柄を使い回している服は全パーツが0になり、
    /// 一番重い服が一番軽く見える（T-8 実機確認）。まとまりごと外した差分を別に測ることでそこに答える。
    /// </para>
    /// <para>
    /// プラットフォーム引数は持たせない。<c>AnalyzeMaterials</c> はビルドターゲットを一切参照しないと
    /// IL 解析で確定済みのため、内訳は PC / Quest で必ず同一になる（W0 設計 §2.5）。
    /// </para>
    /// </summary>
    internal static class PerformanceBreakdownBuilder
    {
        /// <summary>
        /// アバターのパフォーマンス内訳を組み立てる。
        ///
        /// <see cref="SdkPerformanceReflection.IsAvailable"/> が false の場合や、
        /// 内訳対象の Renderer が1件も無い場合は false を返す。
        /// 呼び出し側はこの場合、内訳セクションを何も描かない（W0 設計 §5.2）。
        /// </summary>
        public static bool TryBuild(GameObject avatarRoot, out PerformanceBreakdown breakdown)
        {
            breakdown = null;
            if (avatarRoot == null) return false;
            if (!SdkPerformanceReflection.IsAvailable) return false;

            var renderers = PerformanceReportBuilder.CollectRenderers(avatarRoot);
            if (renderers.Count == 0) return false;

            PerformanceReportBuilder.PrepareSdk(avatarRoot);

            if (!TryMeasureTextureMegabytes(renderers, out var totalTextureMb)) return false;

            var partByRenderer = new Dictionary<Renderer, PerformancePart>(renderers.Count);
            var totalPoly = 0;

            foreach (var renderer in renderers)
            {
                SdkPerformanceReflection.TryGetPolyCount(renderer, out var polyCount);
                totalPoly += polyCount;

                var reductionMb = CalculateTextureReduction(renderers, new List<Renderer> { renderer }, totalTextureMb);

                partByRenderer[renderer] = new PerformancePart(
                    renderer.name,
                    HierarchyPathUtil.GetHierarchyPath(renderer.gameObject),
                    renderer,
                    reductionMb,
                    polyCount,
                    HasNoTexture(new List<Renderer> { renderer }));
            }

            var groups = new List<PerformancePartGroup>();
            foreach (var partGroup in AvatarPartGrouping.Build(avatarRoot, renderers))
            {
                var members = partGroup.Renderers.ToList();
                var parts = members
                    .Where(r => partByRenderer.ContainsKey(r))
                    .Select(r => partByRenderer[r])
                    .OrderByDescending(p => p.PolyCount)
                    .ToList();

                var groupPoly = parts.Sum(p => p.PolyCount);

                // アバター本体は外せないので、まとまりの削減量は測らない（0のまま・W0 設計 §11.2）
                var groupReduction = partGroup.IsAvatarBody
                    ? 0f
                    : CalculateTextureReduction(renderers, members, totalTextureMb);

                groups.Add(new PerformancePartGroup(
                    partGroup.Name,
                    partGroup.Root,
                    partGroup.IsAvatarBody,
                    groupReduction,
                    groupPoly,
                    HasNoTexture(members),
                    parts));
            }

            var orderedParts = partByRenderer.Values
                .OrderByDescending(p => p.TextureReductionMegabytes)
                .ToList();

            breakdown = new PerformanceBreakdown(OrderGroups(groups), orderedParts, totalTextureMb, totalPoly);
            return true;
        }

        /// <summary>
        /// まとまりの並び: 「外すと減るテクスチャ」降順 → 同点はポリゴン降順。
        /// アバター本体は外す対象ではないので常に最後（W0 設計 §11.2）。
        /// </summary>
        internal static List<PerformancePartGroup> OrderGroups(List<PerformancePartGroup> groups)
        {
            return groups
                .OrderBy(g => g.IsAvatarBody ? 1 : 0)
                .ThenByDescending(g => g.TextureReductionMegabytes)
                .ThenByDescending(g => g.PolyCount)
                .ToList();
        }

        /// <summary>
        /// <paramref name="target"/> を除いた集合でテクスチャ使用量を測り直し、総量との差分を削減量として返す。
        /// 除いた結果が空（対象がアバターの全 Renderer）の場合は、総量そのものが削減量になる。
        /// 測り直しに失敗した場合は 0 を返す（そこだけ「減りません」扱いになるが、他の計算は続ける）。
        /// </summary>
        private static float CalculateTextureReduction(
            IReadOnlyList<Renderer> allRenderers, IReadOnlyList<Renderer> target, float totalTextureMegabytes)
        {
            var excluded = new HashSet<Renderer>(target);
            var remaining = allRenderers.Where(r => !excluded.Contains(r)).ToList();
            if (remaining.Count == 0) return totalTextureMegabytes;

            if (!TryMeasureTextureMegabytes(remaining, out var withoutMb)) return 0f;

            return ReductionFromDiff(totalTextureMegabytes, withoutMb);
        }

        /// <summary>
        /// その対象だけを SDK に測らせて 0MB かどうか（＝テクスチャを1枚も持たないか）。
        /// 自前で材質を数えない（DEC-069 決定8）。測れなかったときは「テクスチャなし」と断定しない。
        /// </summary>
        private static bool HasNoTexture(IReadOnlyList<Renderer> target)
        {
            if (target == null || target.Count == 0) return false;
            if (!TryMeasureTextureMegabytes(target.ToList(), out var soloMb)) return false;
            return soloMb <= 0f;
        }

        private static bool TryMeasureTextureMegabytes(List<Renderer> renderers, out float megabytes)
        {
            megabytes = 0f;
            var stats = new AvatarPerformanceStats(false);
            if (!SdkPerformanceReflection.TryAnalyzeMaterials(renderers, stats)) return false;
            return PerformanceCategoryLabels.TryGetNumericValue(stats, "textureMegabytes", out megabytes);
        }

        /// <summary>
        /// 「総量」と「除いたときの量」の差分から削減量を出す純粋関数。
        /// SDK 呼び出しから切り離してあるのは、SDK 未インストール環境でも
        /// 差分方式の帰属ロジック自体をテストできるようにするため（単独帰属に化けていないことの確認用）。
        /// 浮動小数の丸め誤差でわずかに負になるケースは 0 に丸める。
        /// </summary>
        internal static float ReductionFromDiff(float totalValue, float valueWithoutPart)
        {
            var reduction = totalValue - valueWithoutPart;
            return reduction > 0f ? reduction : 0f;
        }
    }
}
