using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.SDKBase.Validation.Performance;
using VRC.SDKBase.Validation.Performance.Stats;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// VRChat SDK のパフォーマンス計測 API を呼び出し、UI が使う <see cref="AvatarPerformanceReport"/> を組み立てる。
    /// SDK API への依存はこのクラスに閉じ込める。
    ///
    /// <para>
    /// 数値はすべて SDK の返り値をそのまま使い、自前計算はしない（DEC-069 決定事項8）。
    /// ランクの閾値も <c>AvatarPerformanceStats.GetStatLevelForRating()</c> から取得し、自前の閾値表は持たない。
    /// </para>
    /// </summary>
    internal static class PerformanceReportBuilder
    {
        /// <summary>
        /// ランクの良い順。
        /// 列挙値の数値の並びには依存せず、必ずこの配列上のインデックスで比較する
        /// （SDK 側で <c>None</c> がどこに置かれているかに実装を左右されないようにするため）。
        /// </summary>
        private static readonly PerformanceRating[] RatingsBestToWorst =
        {
            PerformanceRating.Excellent,
            PerformanceRating.Good,
            PerformanceRating.Medium,
            PerformanceRating.Poor,
            PerformanceRating.VeryPoor,
        };

        /// <summary>ランクの「悪さ」を表す 0（Excellent）〜4（Very Poor）の順位。未知のランクは -1。</summary>
        internal static int RatingOrder(PerformanceRating rating) => Array.IndexOf(RatingsBestToWorst, rating);

        /// <summary>
        /// アバターを計測し、PC / Quest 両方の結果と Quest 非対応要因をまとめて返す。
        /// 失敗しても例外は投げず、<see cref="AvatarPerformanceReport.FailureReason"/> に理由を入れて返す。
        /// </summary>
        public static AvatarPerformanceReport Build(GameObject avatarRoot)
        {
            if (avatarRoot == null)
                return new AvatarPerformanceReport(null, null, null, "アバターが指定されていません。");

            try
            {
                PrepareSdk(avatarRoot);

                var pcStats = Calculate(avatarRoot, isMobile: false);
                var questStats = Calculate(avatarRoot, isMobile: true);

                var pc = BuildPlatform(PerformancePlatform.PC, pcStats, isMobile: false);
                var quest = BuildPlatform(PerformancePlatform.Quest, questStats, isMobile: true);
                var incompatibilities = QuestCompatibilityScanner.Scan(avatarRoot);

                return new AvatarPerformanceReport(pc, quest, incompatibilities);
            }
            catch (Exception e)
            {
                // パフォーマンス表示は「おまけ」なので、失敗してもチェック本体は止めない
                Debug.LogWarning($"[AvatarOmamori] パフォーマンスの計測に失敗しました。{e}");
                return new AvatarPerformanceReport(null, null, null, "パフォーマンスを計測できませんでした。");
            }
        }

        /// <summary>
        /// 計測前に必要な SDK 側の準備（T-1 実機検証で判明した必須手順）。
        /// </summary>
        private static void PrepareSdk(GameObject avatarRoot)
        {
            // ランク閾値（LevelSet）の読み込み。通常は SDK の EnvConfig が InitializeOnLoad 経由で呼ぶが、
            // そのタイミングより前に計算すると CalculatePerformanceStats() が NullReferenceException になる。
            try
            {
                AvatarPerformanceStats.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] AvatarPerformanceStats.Initialize() に失敗しました。{e.Message}");
            }

            // 未登録の VRChat Constraint があると Constraint 系の値が不正確になる。
            // SDK のビルドパネルも計算直前に同じ処理を行っている。
            RefreshConstraintGroups(avatarRoot);
        }

        /// <summary>
        /// <c>VRC.Dynamics.VRCConstraintManager.Sdk_ManuallyRefreshGroups()</c> をリフレクション経由で呼ぶ。
        /// VRChat Constraints は SDK のバージョンによって存在しないことがあるため、
        /// 型が見つからない場合は静かにスキップする。
        /// </summary>
        private static void RefreshConstraintGroups(GameObject avatarRoot)
        {
            try
            {
                var constraintBaseType = FindType("VRC.Dynamics.VRCConstraintBase");
                var managerType = FindType("VRC.Dynamics.VRCConstraintManager");
                if (constraintBaseType == null || managerType == null) return;

                var refresh = managerType.GetMethod(
                    "Sdk_ManuallyRefreshGroups", BindingFlags.Public | BindingFlags.Static);
                if (refresh == null) return;

                var getComponents = typeof(GameObject)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == "GetComponentsInChildren"
                                && m.IsGenericMethod
                                && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(bool))
                    .MakeGenericMethod(constraintBaseType);

                var constraints = getComponents.Invoke(avatarRoot, new object[] { true });
                refresh.Invoke(null, new[] { constraints });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] Constraint グループの更新に失敗しました。{e.Message}");
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            return null;
        }

        private static AvatarPerformanceStats Calculate(GameObject avatarRoot, bool isMobile)
        {
            var stats = new AvatarPerformanceStats(isMobile);
            AvatarPerformance.CalculatePerformanceStats(avatarRoot.name, avatarRoot, stats, isMobile);
            return stats;
        }

        private static PlatformPerformance BuildPlatform(
            PerformancePlatform platform, AvatarPerformanceStats stats, bool isMobile)
        {
            if (!TryGetRating(stats, AvatarPerformanceCategory.Overall, out var overall)
                || RatingOrder(overall) < 0)
            {
                return new PlatformPerformance(platform, "-", false, false, false, null);
            }

            var factors = BuildFactors(stats, overall, isMobile);

            return new PlatformPerformance(
                platform,
                PerformanceCategoryLabels.FormatRating(overall),
                RatingOrder(overall) >= RatingOrder(PerformanceRating.Medium),
                RatingOrder(overall) == 0, // Excellent＝これ以上は上がらない
                true,
                factors);
        }

        /// <summary>
        /// 総合ランクを下げている項目を、改善効果の大きい順（超過率の降順）に並べて返す。
        /// </summary>
        private static List<PerformanceFactor> BuildFactors(
            AvatarPerformanceStats stats, PerformanceRating overall, bool isMobile)
        {
            var factors = new List<PerformanceFactor>();
            var overallOrder = RatingOrder(overall);
            if (overallOrder <= 0) return factors; // すでに Excellent。これ以上は上がらない

            foreach (var entry in PerformanceCategoryLabels.Entries)
            {
                if (!TryGetRating(stats, entry.Category, out var rating)) continue;

                var ratingOrder = RatingOrder(rating);
                if (ratingOrder < 0) continue; // None など、ランク判定の対象外

                // 総合ランクは各カテゴリの最悪値なので、ここを通るのは実質「総合ランクと同じランクの項目」。
                // それらを1つ上のランクの範囲まで下げると、総合ランクが1つ上がる
                if (ratingOrder < overallOrder) continue;

                if (!PerformanceCategoryLabels.TryGetNumericValue(stats, entry.FieldPath, out var currentValue))
                    continue;

                var target = FindNextTarget(entry, currentValue, overall, isMobile);

                factors.Add(new PerformanceFactor(
                    entry.Label,
                    PerformanceCategoryLabels.FormatValue(currentValue, entry.Format),
                    target.HasValue ? PerformanceCategoryLabels.FormatValue(target.Value.Limit, entry.Format) : null,
                    target.HasValue ? PerformanceCategoryLabels.FormatRating(target.Value.Rating) : null,
                    target.HasValue ? ExcessRatio(currentValue, target.Value.Limit) : 1f,
                    PerformanceCategoryLabels.PerformanceDocUrl));
            }

            return factors
                .OrderByDescending(f => f.ExcessRatio)
                .ThenBy(f => f.Label, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 「1ランク上げるために満たすべき上限値」を探す。
        ///
        /// <para>
        /// 閾値には同値の段がある（PC のポリゴン数は Good / Medium / Poor がすべて 70,000 など）。
        /// 単純に1つ上のランクの閾値を出すと「あと0」になってしまうため、
        /// <b>現在値より実際に小さい閾値</b>が現れるまで、良いランク方向へ辿る。
        /// </para>
        /// </summary>
        internal static (float Limit, PerformanceRating Rating)? FindNextTarget(
            PerformanceCategoryLabels.Entry entry, float currentValue, PerformanceRating overall, bool isMobile)
        {
            // 総合ランクの1つ上から順に、良い方向へ見ていく
            var startIndex = RatingOrder(overall) - 1;
            for (var i = startIndex; i >= 0; i--)
            {
                var rating = RatingsBestToWorst[i];
                object level;
                try
                {
                    level = AvatarPerformanceStats.GetStatLevelForRating(rating, isMobile);
                }
                catch (Exception)
                {
                    return null;
                }

                if (!PerformanceCategoryLabels.TryGetNumericValue(level, entry.FieldPath, out var limit)) return null;
                if (limit < currentValue) return (limit, rating);
            }

            return null;
        }

        internal static float ExcessRatio(float current, float limit)
        {
            // 上限が 0 の項目（Quest のライト・Audio Source 等）は「0 にするしかない」＝最優先で見せたい
            if (limit <= 0f) return float.MaxValue;
            return current / limit;
        }

        private static bool TryGetRating(
            AvatarPerformanceStats stats, AvatarPerformanceCategory category, out PerformanceRating rating)
        {
            rating = PerformanceRating.None;
            if (stats == null) return false;

            try
            {
                rating = stats.GetPerformanceRatingForCategory(category);
                return true;
            }
            catch (Exception)
            {
                // AvatarPerformanceCategoryCount のような番兵値では IndexOutOfRangeException になる
                return false;
            }
        }
    }
}
