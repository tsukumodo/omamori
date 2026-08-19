namespace AvatarOmamori.Editor
{
    /// <summary>
    /// 「調べる」ボタンから開く外部ドキュメント（VRChat 公式・Modular Avatar 公式）のURL定数を集約するクラス。
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

        /// <summary>
        /// アバターのパラメータ解説。同期パラメータの上限（256 bit）が明記されている唯一のページなので、
        /// 同期パラメータ上限チェックの案内先はここにする（Expressions Menu 側のページには上限の記載が無い）。
        /// </summary>
        public const string AnimatorParameters = "https://creators.vrchat.com/avatars/animator-parameters/";

        /// <summary>Modular Avatar の Merge Armature の解説（衣装をアバターの Armature に統合する側）。</summary>
        public const string MaMergeArmature = "https://modular-avatar.nadena.dev/ja/docs/reference/merge-armature";

        /// <summary>Expression Menu / Controls の解説。</summary>
        public const string ExpressionsMenu = "https://creators.vrchat.com/avatars/expression-menu-and-controls/";

        /// <summary>Modular Avatar の Bone Proxy の解説（特定ボーンへの追従）。</summary>
        public const string MaBoneProxy = "https://modular-avatar.nadena.dev/ja/docs/reference/bone-proxy";

        /// <summary>Modular Avatar の Menu Installer の解説（Menu Item をアバターのメニューに接続する側）。</summary>
        public const string MaMenuInstaller = "https://modular-avatar.nadena.dev/ja/docs/reference/menu-installer";

        /// <summary>
        /// Modular Avatar の Object Toggle の解説（他のオブジェクトの表示/非表示を切り替える側）。
        /// 公式サイトの改編で <c>docs/reference/</c> 直下から <c>docs/reference/reaction/</c> へ移っているため、
        /// 他の MA 系リンクとパスの形が違う。
        /// </summary>
        public const string MaObjectToggle = "https://modular-avatar.nadena.dev/ja/docs/reference/reaction/object-toggle";

        /// <summary>
        /// VRChat がアバターで許可しているコンポーネントの一覧。
        /// ここに無いものはアップロード時に取り除かれる。
        /// </summary>
        public const string AllowedAvatarComponents =
            "https://creators.vrchat.com/avatars/whitelisted-avatar-components/whitelisted-avatar-components/";
    }
}
