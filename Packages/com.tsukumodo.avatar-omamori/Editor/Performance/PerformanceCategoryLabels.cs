using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using VRC.SDKBase.Validation.Performance;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// <see cref="AvatarPerformanceCategory"/> と、おまもりの表示に必要な情報（日本語名・値の取り出し方・書式）の対応表。
    ///
    /// <para>
    /// SDK の <c>AvatarPerformanceStats.GetPerformanceCategoryDisplayName()</c> は使わない。
    /// <c>Overall</c> / <c>None</c> / <c>AvatarPerformanceCategoryCount</c> で <see cref="KeyNotFoundException"/>
    /// を投げるうえ、表示は日本語にしたいため（T-1 実機検証で確認）。
    /// </para>
    /// <para>
    /// 値の取り出しはフィールド名によるリフレクションで行う。<c>AvatarPerformanceStats</c>（現在値・Nullable）と
    /// <c>AvatarPerformanceStatsLevel</c>（ランク閾値）で同じフィールド名が使えるため、1つの経路で両方を扱える。
    /// SDK 更新でフィールド名が変わった場合は、その項目だけが静かに内訳から外れる（例外にはしない）。
    /// </para>
    /// </summary>
    internal static class PerformanceCategoryLabels
    {
        /// <summary>
        /// パフォーマンスランク全体の解説（層B の「調べる」リンク先のフォールバック）。
        /// このページには項目ごとのアンカーが無い（見出しは #pc-limits 等の粒度のみ）ため、
        /// 専用の解説ページが無いカテゴリだけがここに落ちる。
        /// </summary>
        public const string PerformanceDocUrl = OmamoriDocUrls.PerformanceRanking;

        /// <summary>Quest / iOS のコンテンツ制限（層A の「調べる」リンク先）。</summary>
        public const string QuestDocUrl = OmamoriDocUrls.QuestContentLimitations;

        /// <summary>アバター最適化のヒント（項目別の解説はこのページのアンカーに散らばっている）。</summary>
        private const string OptimizingTipsUrl = OmamoriDocUrls.AvatarOptimizingTips;

        private const string MeshDocUrl = OptimizingTipsUrl + "#reduce-the-amount-of-meshes-on-your-avatar";
        private const string TextureDocUrl = OptimizingTipsUrl + "#watch-your-vram-usage";
        private const string MaterialDocUrl = OptimizingTipsUrl + "#reduce-the-amount-of-material-slots-you-use";
        private const string BoneDocUrl = OptimizingTipsUrl + "#reduce-the-amount-of-bones";

        // "amountamount" はタイポに見えるが誤記ではない。VRChat公式ページの見出し
        // 「Reduce the emission amount/amount of particle systems」から生成された実際のアンカー。
        // 親切心で直すとリンク切れになるので触らないこと
        private const string ParticleDocUrl = OptimizingTipsUrl + "#reduce-the-emission-amountamount-of-particle-systems";

        private const string LightDocUrl = OptimizingTipsUrl + "#limit-the-number-of-lights-your-avatar-uses";
        private const string ClothDocUrl = OptimizingTipsUrl + "#limit-usage-of-cloth";

        /// <summary>PhysBone の解説。</summary>
        public const string PhysBoneDocUrl = OmamoriDocUrls.PhysBones;
        private const string PhysBoneColliderDocUrl = PhysBoneDocUrl + "#vrcphysbonecollider";

        /// <summary>Contact の解説。</summary>
        private const string ContactDocUrl = "https://creators.vrchat.com/avatars/avatar-dynamics/contacts";

        /// <summary>VRChat Constraints の解説（Unity Constraint からの移行案内を含む）。</summary>
        public const string ConstraintDocUrl = OmamoriDocUrls.Constraints;

        /// <summary>値の書式。</summary>
        public enum ValueFormat
        {
            /// <summary>整数（3桁区切り）。</summary>
            Count,

            /// <summary>メガバイト（小数1桁 + " MB"）。</summary>
            Megabytes
        }

        /// <summary>1カテゴリ分の表示定義。</summary>
        public sealed class Entry
        {
            public AvatarPerformanceCategory Category { get; }
            public string Label { get; }
            public string FieldPath { get; }
            public ValueFormat Format { get; }

            /// <summary>「調べる」ボタンで開く、この項目の専用解説ページ。専用ページが無い場合は <see cref="PerformanceDocUrl"/>。</summary>
            public string DocumentUrl { get; }

            // 既定値を付けない。項目追加時に URL 指定を忘れると全項目が同じリンクに戻ってしまうため、
            // コンパイル時に必ず明示させる
            public Entry(
                AvatarPerformanceCategory category, string label, string fieldPath, ValueFormat format, string documentUrl)
            {
                Category = category;
                Label = label;
                FieldPath = fieldPath;
                Format = format;
                DocumentUrl = documentUrl;
            }
        }

        /// <summary>
        /// 内訳に出す数値カテゴリの一覧。
        ///
        /// 意図的に含めていないもの:
        /// ・<c>DownloadSize</c> / <c>UncompressedSize</c> … 実ビルドしないと確定せず、エディタ上では常に null（T-1 実測）
        /// ・<c>AABB</c> … Bounds なので「あと何を減らす」の表現になじまない
        /// ・<c>ParticleTrailsEnabled</c> / <c>ParticleCollisionEnabled</c> … bool のため同上
        /// ・<c>Overall</c> / <c>None</c> / <c>AvatarPerformanceCategoryCount</c> … 集計用・番兵
        /// </summary>
        public static readonly IReadOnlyList<Entry> Entries = new List<Entry>
        {
            new Entry(AvatarPerformanceCategory.PolyCount, "ポリゴン数", "polyCount", ValueFormat.Count, MeshDocUrl),
            new Entry(AvatarPerformanceCategory.TextureMegabytes, "テクスチャ使用量", "textureMegabytes", ValueFormat.Megabytes, TextureDocUrl),
            new Entry(AvatarPerformanceCategory.MaterialCount, "マテリアルスロット数", "materialCount", ValueFormat.Count, MaterialDocUrl),
            new Entry(AvatarPerformanceCategory.SkinnedMeshCount, "スキンメッシュ数", "skinnedMeshCount", ValueFormat.Count, MeshDocUrl),
            new Entry(AvatarPerformanceCategory.MeshCount, "メッシュ数", "meshCount", ValueFormat.Count, MeshDocUrl),
            new Entry(AvatarPerformanceCategory.BoneCount, "ボーン数", "boneCount", ValueFormat.Count, BoneDocUrl),
            new Entry(AvatarPerformanceCategory.AnimatorCount, "Animator 数", "animatorCount", ValueFormat.Count, PerformanceDocUrl),
            new Entry(AvatarPerformanceCategory.PhysBoneComponentCount, "PhysBone の数", "physBone.componentCount", ValueFormat.Count, PhysBoneDocUrl),
            new Entry(AvatarPerformanceCategory.PhysBoneTransformCount, "PhysBone が動かすボーン数", "physBone.transformCount", ValueFormat.Count, PhysBoneDocUrl),
            new Entry(AvatarPerformanceCategory.PhysBoneColliderCount, "PhysBone コライダー数", "physBone.colliderCount", ValueFormat.Count, PhysBoneColliderDocUrl),
            new Entry(AvatarPerformanceCategory.PhysBoneCollisionCheckCount, "PhysBone の衝突判定数", "physBone.collisionCheckCount", ValueFormat.Count, PhysBoneColliderDocUrl),
            new Entry(AvatarPerformanceCategory.ContactCount, "Contact の数", "contactCount", ValueFormat.Count, ContactDocUrl),
            // ⚠ ConstraintsCount / ConstraintDepth は VRChat Constraints（SDK 3.7.0）で追加された項目。
            //    これより古い SDK では列挙値が存在せずパッケージ全体がコンパイルできないため、
            //    package.json の vpmDependencies を >=3.7.0 未満に戻さないこと
            new Entry(AvatarPerformanceCategory.ConstraintsCount, "Constraint の数", "constraintsCount", ValueFormat.Count, ConstraintDocUrl),
            new Entry(AvatarPerformanceCategory.ConstraintDepth, "Constraint の深さ", "constraintDepth", ValueFormat.Count, ConstraintDocUrl),
            new Entry(AvatarPerformanceCategory.ParticleSystemCount, "パーティクルシステム数", "particleSystemCount", ValueFormat.Count, ParticleDocUrl),
            new Entry(AvatarPerformanceCategory.ParticleTotalCount, "パーティクル総数", "particleTotalCount", ValueFormat.Count, ParticleDocUrl),
            new Entry(AvatarPerformanceCategory.ParticleMaxMeshPolyCount, "メッシュパーティクルのポリゴン数", "particleMaxMeshPolyCount", ValueFormat.Count, ParticleDocUrl),
            new Entry(AvatarPerformanceCategory.TrailRendererCount, "Trail Renderer の数", "trailRendererCount", ValueFormat.Count, PerformanceDocUrl),
            new Entry(AvatarPerformanceCategory.LineRendererCount, "Line Renderer の数", "lineRendererCount", ValueFormat.Count, PerformanceDocUrl),
            new Entry(AvatarPerformanceCategory.LightCount, "ライトの数", "lightCount", ValueFormat.Count, LightDocUrl),
            new Entry(AvatarPerformanceCategory.AudioSourceCount, "Audio Source の数", "audioSourceCount", ValueFormat.Count, PerformanceDocUrl),
            new Entry(AvatarPerformanceCategory.ClothCount, "Cloth の数", "clothCount", ValueFormat.Count, ClothDocUrl),
            new Entry(AvatarPerformanceCategory.ClothMaxVertices, "Cloth の頂点数", "clothMaxVertices", ValueFormat.Count, ClothDocUrl),
            new Entry(AvatarPerformanceCategory.PhysicsColliderCount, "物理コライダー数", "physicsColliderCount", ValueFormat.Count, PerformanceDocUrl),
            new Entry(AvatarPerformanceCategory.PhysicsRigidbodyCount, "Rigidbody の数", "physicsRigidbodyCount", ValueFormat.Count, PerformanceDocUrl),
            new Entry(AvatarPerformanceCategory.RaycastCount, "Raycast の数", "raycastCount", ValueFormat.Count, PerformanceDocUrl),
        };

        /// <summary>
        /// <paramref name="target"/>（AvatarPerformanceStats / AvatarPerformanceStatsLevel）から
        /// <paramref name="fieldPath"/>（"polyCount" や "physBone.componentCount"）の数値を取り出す。
        /// Nullable は中身を取り出し、null や未知のフィールドは false を返す。
        /// </summary>
        public static bool TryGetNumericValue(object target, string fieldPath, out float value)
        {
            value = 0f;
            if (target == null || string.IsNullOrEmpty(fieldPath)) return false;

            object current = target;
            foreach (var segment in fieldPath.Split('.'))
            {
                current = ReadMember(current, segment);
                if (current == null) return false;
            }

            // Convert.ToSingle(bool) は例外にならず true→1 / false→0 を返してしまうため、
            // ここで明示的に弾く（下の catch は Bounds など変換不能な型が対象で、bool はすり抜けてしまう）
            if (current is bool) return false;

            try
            {
                value = Convert.ToSingle(current, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                // 数値に変換できない型（Bounds 等）は内訳の対象外
                return false;
            }
        }

        /// <summary>
        /// フィールド（またはプロパティ）を1段読む。Nullable&lt;T&gt; は中身に展開して返す。
        /// </summary>
        private static object ReadMember(object target, string name)
        {
            if (target == null) return null;

            var type = target.GetType();
            object raw = null;

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                raw = field.GetValue(target);
            }
            else
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property == null) return null;
                raw = property.GetValue(target);
            }

            // Nullable<T> は GetValue の時点で「中身 or null」にボックス化されるため追加処理は不要
            return raw;
        }

        /// <summary>数値を表示用の文字列にする。</summary>
        public static string FormatValue(float value, ValueFormat format)
        {
            switch (format)
            {
                case ValueFormat.Megabytes:
                    return value.ToString("0.#", CultureInfo.InvariantCulture) + " MB";
                default:
                    return Mathf_RoundToInt(value).ToString("#,0", CultureInfo.InvariantCulture);
            }
        }

        // UnityEngine.Mathf.RoundToInt は銀行家丸め（0.5 → 偶数側）なので、
        // 表示上の期待と揃うよう AwayFromZero で丸める
        private static int Mathf_RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>SDK のランク列挙を、表示用の文字列にする（SDK・公式ドキュメントと表記を揃えるため英語のまま）。</summary>
        public static string FormatRating(PerformanceRating rating)
        {
            switch (rating)
            {
                case PerformanceRating.Excellent: return "Excellent";
                case PerformanceRating.Good: return "Good";
                case PerformanceRating.Medium: return "Medium";
                case PerformanceRating.Poor: return "Poor";
                case PerformanceRating.VeryPoor: return "Very Poor";
                default: return "-";
            }
        }
    }
}
