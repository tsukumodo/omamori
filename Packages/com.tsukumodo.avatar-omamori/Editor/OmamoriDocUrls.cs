namespace AvatarOmamori.Editor
{
    /// <summary>
    /// VRChat公式ドキュメントのURL定数を集約するクラス。
    /// 複数のチェックが同じURLを参照するようになるため、リンク切れ時にここ1箇所を直せばよいようにする。
    /// </summary>
    public static class OmamoriDocUrls
    {
        /// <summary>
        /// パフォーマンスランク全体の解説（層B の「調べる」リンク先のフォールバック）。
        /// このページには項目ごとのアンカーが無い（見出しは #pc-limits 等の粒度のみ）ため、
        /// 専用の解説ページが無いカテゴリだけがここに落ちる。
        /// </summary>
        public const string PerformanceRanking =
            "https://creators.vrchat.com/avatars/avatar-performance-ranking-system";

        /// <summary>Quest / iOS のコンテンツ制限（層A の「調べる」リンク先）。</summary>
        public const string QuestContentLimitations =
            "https://creators.vrchat.com/platforms/android/quest-content-limitations";

        /// <summary>PhysBone の解説。</summary>
        public const string PhysBones = "https://creators.vrchat.com/avatars/avatar-dynamics/physbones";

        /// <summary>VRChat Constraints の解説（Unity Constraint からの移行案内を含む）。</summary>
        public const string Constraints = "https://creators.vrchat.com/avatars/avatar-dynamics/constraints";

        /// <summary>
        /// アバター最適化のヒント（項目別の解説はこのページのアンカーに散らばっている）。
        /// アンカーを足して使う想定のため、末尾にスラッシュを付けたまま置く。
        /// </summary>
        public const string AvatarOptimizingTips = "https://creators.vrchat.com/avatars/avatar-optimizing-tips/";
    }
}
