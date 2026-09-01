using System.Collections.Generic;
using System.Linq;
using AvatarOmamori.Editor.Util;
using UnityEngine;
using VRC.SDKBase.Validation.Performance.Stats;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// パーツ別パフォーマンス内訳（差分方式）を組み立てる。
    ///
    /// <para>
    /// 単独帰属（そのパーツだけを測る）は採らない。共有テクスチャが多重計上され、
    /// 合計が実際の数倍に水増しされるため（8/8 実測: 850.3MB vs 実際 336.8MB・v0.11.0 W0 設計 §2.2）。
    /// 「全 Renderer で測った総量」から「そのパーツを除いた集合で測り直した量」を引いた差分を、
    /// そのパーツの削減量として扱う（<c>AnalyzeMaterials</c> を N+1 回呼ぶ形になる）。
    /// </para>
    /// <para>
    /// プラットフォーム引数は持たせない。<c>AnalyzeMaterials</c> はビルドターゲットを一切参照しないと
    /// IL 解析で確定済みのため、内訳は PC / Quest で必ず同一になる（v0.11.0 W0 設計 §2.5）。
    /// </para>
    /// </summary>
    internal static class PerformanceBreakdownBuilder
    {
        /// <summary>
        /// アバターのパフォーマンス内訳を組み立てる。
        ///
        /// <see cref="SdkPerformanceReflection.IsAvailable"/> が false の場合や、
        /// 内訳対象の Renderer が1件も無い場合は false を返す。
        /// 呼び出し側はこの場合、内訳セクションを何も描かない（v0.11.0 W0 設計 §5.2）。
        /// </summary>
        public static bool TryBuild(GameObject avatarRoot, out PerformanceBreakdown breakdown)
        {
            breakdown = null;
            if (avatarRoot == null) return false;
            if (!SdkPerformanceReflection.IsAvailable) return false;

            var renderers = PerformanceReportBuilder.CollectRenderers(avatarRoot);
            if (renderers.Count == 0) return false;

            PerformanceReportBuilder.PrepareSdk(avatarRoot);

            var totalStats = new AvatarPerformanceStats(false);
            if (!SdkPerformanceReflection.TryAnalyzeMaterials(renderers, totalStats)) return false;
            if (!PerformanceCategoryLabels.TryGetNumericValue(totalStats, "textureMegabytes", out var totalTextureMb))
                return false;

            var parts = new List<PerformancePart>(renderers.Count);
            var totalPoly = 0;

            foreach (var renderer in renderers)
            {
                SdkPerformanceReflection.TryGetPolyCount(renderer, out var polyCount);
                totalPoly += polyCount;

                var reductionMb = CalculateTextureReduction(renderers, renderer, totalTextureMb);

                parts.Add(new PerformancePart(
                    renderer.name,
                    HierarchyPathUtil.GetHierarchyPath(renderer.gameObject),
                    renderer,
                    reductionMb,
                    polyCount));
            }

            var ordered = parts.OrderByDescending(p => p.TextureReductionMegabytes).ToList();
            breakdown = new PerformanceBreakdown(ordered, totalTextureMb, totalPoly);
            return true;
        }

        /// <summary>
        /// <paramref name="target"/> を除いた集合でテクスチャ使用量を測り直し、総量との差分を削減量として返す。
        /// 除いた集合が空（Renderer が1件しかない）場合は、総量そのものが削減量になる。
        /// 測り直しに失敗した場合は 0 を返す（そのパーツだけ「減りません」扱いになるが、他のパーツの計算は続ける）。
        /// </summary>
        private static float CalculateTextureReduction(
            IReadOnlyList<Renderer> allRenderers, Renderer target, float totalTextureMegabytes)
        {
            var remaining = allRenderers.Where(r => !ReferenceEquals(r, target)).ToList();
            if (remaining.Count == 0) return totalTextureMegabytes;

            var statsWithout = new AvatarPerformanceStats(false);
            if (!SdkPerformanceReflection.TryAnalyzeMaterials(remaining, statsWithout)) return 0f;
            if (!PerformanceCategoryLabels.TryGetNumericValue(statsWithout, "textureMegabytes", out var withoutMb))
                return 0f;

            return ReductionFromDiff(totalTextureMegabytes, withoutMb);
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
