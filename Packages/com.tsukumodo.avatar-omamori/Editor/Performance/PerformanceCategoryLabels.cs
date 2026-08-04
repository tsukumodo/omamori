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
        /// <summary>パフォーマンスランク全体の解説（層B の「調べる」リンク先）。</summary>
        public const string PerformanceDocUrl =
            "https://creators.vrchat.com/avatars/avatar-performance-ranking-system";

        /// <summary>Quest / iOS のコンテンツ制限（層A の「調べる」リンク先）。</summary>
        public const string QuestDocUrl =
            "https://creators.vrchat.com/platforms/android/quest-content-limitations";

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

            public Entry(AvatarPerformanceCategory category, string label, string fieldPath, ValueFormat format)
            {
                Category = category;
                Label = label;
                FieldPath = fieldPath;
                Format = format;
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
            new Entry(AvatarPerformanceCategory.PolyCount, "ポリゴン数", "polyCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.TextureMegabytes, "テクスチャ使用量", "textureMegabytes", ValueFormat.Megabytes),
            new Entry(AvatarPerformanceCategory.MaterialCount, "マテリアルスロット数", "materialCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.SkinnedMeshCount, "スキンメッシュ数", "skinnedMeshCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.MeshCount, "メッシュ数", "meshCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.BoneCount, "ボーン数", "boneCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.AnimatorCount, "Animator 数", "animatorCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.PhysBoneComponentCount, "PhysBone の数", "physBone.componentCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.PhysBoneTransformCount, "PhysBone が動かすボーン数", "physBone.transformCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.PhysBoneColliderCount, "PhysBone コライダー数", "physBone.colliderCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.PhysBoneCollisionCheckCount, "PhysBone の衝突判定数", "physBone.collisionCheckCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ContactCount, "Contact の数", "contactCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ConstraintsCount, "Constraint の数", "constraintsCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ConstraintDepth, "Constraint の深さ", "constraintDepth", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ParticleSystemCount, "パーティクルシステム数", "particleSystemCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ParticleTotalCount, "パーティクル総数", "particleTotalCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ParticleMaxMeshPolyCount, "メッシュパーティクルのポリゴン数", "particleMaxMeshPolyCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.TrailRendererCount, "Trail Renderer の数", "trailRendererCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.LineRendererCount, "Line Renderer の数", "lineRendererCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.LightCount, "ライトの数", "lightCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.AudioSourceCount, "Audio Source の数", "audioSourceCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ClothCount, "Cloth の数", "clothCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.ClothMaxVertices, "Cloth の頂点数", "clothMaxVertices", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.PhysicsColliderCount, "物理コライダー数", "physicsColliderCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.PhysicsRigidbodyCount, "Rigidbody の数", "physicsRigidbodyCount", ValueFormat.Count),
            new Entry(AvatarPerformanceCategory.RaycastCount, "Raycast の数", "raycastCount", ValueFormat.Count),
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

            try
            {
                value = Convert.ToSingle(current, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                // 数値に変換できない型（bool / Bounds 等）は内訳の対象外
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

        // UnityEngine への依存をこのファイルに持ち込まないための小さなヘルパー
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
