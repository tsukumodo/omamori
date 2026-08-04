using System.Collections.Generic;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// パフォーマンス表示の対象プラットフォーム。
    /// VRChat 公式は Android / Quest / iOS を "Mobile" として1本のガイドラインで扱うため、
    /// おまもりも PC / Quest の2値だけを持つ（iOS 固有分岐は作らない）。
    /// </summary>
    public enum PerformancePlatform
    {
        /// <summary>PC（Windows スタンドアロン）。</summary>
        PC,

        /// <summary>Quest / iOS（VRChat の Mobile 区分）。</summary>
        Quest
    }

    /// <summary>
    /// 総合ランクを下げている要因1件分。
    /// 「あと何をどこまで減らすと1ランク上がるか」を表示するのに必要な情報だけを持つ。
    /// </summary>
    public sealed class PerformanceFactor
    {
        /// <summary>日本語の項目名（例: "テクスチャ使用量"）。</summary>
        public string Label { get; }

        /// <summary>SDK が返した現在値の表示文字列（例: "176.5 MB"）。</summary>
        public string CurrentText { get; }

        /// <summary>
        /// 1ランク上げるために満たすべき上限値の表示文字列（例: "150 MB"）。
        /// 目標ランクが存在しない（すでに最高ランク等）場合は null。
        /// </summary>
        public string TargetText { get; }

        /// <summary>目標ランクの表示名（例: "Poor"）。<see cref="TargetText"/> が null なら null。</summary>
        public string TargetRatingName { get; }

        /// <summary>
        /// 超過の度合い（現在値 ÷ 目標値）。並び順（＝改善効果の大きい順）の決定にのみ使う。
        /// 目標値が 0 のときは大きな値を入れて先頭に来るようにする。
        /// </summary>
        public float ExcessRatio { get; }

        /// <summary>この項目の解説を開く公式ドキュメントの URL。</summary>
        public string DocumentUrl { get; }

        public PerformanceFactor(
            string label,
            string currentText,
            string targetText,
            string targetRatingName,
            float excessRatio,
            string documentUrl)
        {
            Label = label;
            CurrentText = currentText;
            TargetText = targetText;
            TargetRatingName = targetRatingName;
            ExcessRatio = excessRatio;
            DocumentUrl = documentUrl;
        }
    }

    /// <summary>
    /// 1プラットフォーム分の計測結果。
    /// </summary>
    public sealed class PlatformPerformance
    {
        /// <summary>対象プラットフォーム。</summary>
        public PerformancePlatform Platform { get; }

        /// <summary>総合ランクの表示名（例: "Very Poor"）。SDK の PerformanceRating に対応。</summary>
        public string OverallRatingName { get; }

        /// <summary>総合ランクが Medium 以下かどうか。UI の色分けに使う。</summary>
        public bool IsHeavy { get; }

        /// <summary>総合ランクが取得できたかどうか。false ならセクションを描画しない。</summary>
        public bool IsValid { get; }

        /// <summary>ランクを下げている要因（改善効果の大きい順）。</summary>
        public IReadOnlyList<PerformanceFactor> Factors { get; }

        public PlatformPerformance(
            PerformancePlatform platform,
            string overallRatingName,
            bool isHeavy,
            bool isValid,
            IReadOnlyList<PerformanceFactor> factors)
        {
            Platform = platform;
            OverallRatingName = overallRatingName;
            IsHeavy = isHeavy;
            IsValid = isValid;
            Factors = factors ?? new List<PerformanceFactor>();
        }
    }

    /// <summary>
    /// パフォーマンスセクションに表示する内容一式（不変）。
    /// SDK API への依存は <see cref="PerformanceReportBuilder"/> に閉じ込め、UI 側はこのモデルだけを見る。
    /// </summary>
    public sealed class AvatarPerformanceReport
    {
        /// <summary>PC の計測結果。</summary>
        public PlatformPerformance Pc { get; }

        /// <summary>Quest / iOS の計測結果。</summary>
        public PlatformPerformance Quest { get; }

        /// <summary>Quest で使えない要素（層A）の一覧。</summary>
        public IReadOnlyList<QuestIncompatibility> QuestIncompatibilities { get; }

        /// <summary>計測自体に失敗した場合の理由。成功時は null。</summary>
        public string FailureReason { get; }

        /// <summary>計測に成功しているか。</summary>
        public bool IsValid => FailureReason == null;

        public AvatarPerformanceReport(
            PlatformPerformance pc,
            PlatformPerformance quest,
            IReadOnlyList<QuestIncompatibility> questIncompatibilities,
            string failureReason = null)
        {
            Pc = pc;
            Quest = quest;
            QuestIncompatibilities = questIncompatibilities ?? new List<QuestIncompatibility>();
            FailureReason = failureReason;
        }
    }
}
