using System;
using System.Collections.Generic;
using System.Linq;
using AvatarOmamori.Editor.Performance;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// VRChat がアバターで許可していないコンポーネント（アップロード時に取り除かれるもの）を報告する。
    ///
    /// <para>
    /// 判定は <see cref="QuestCompatibilityScanner"/> に委譲し、
    /// <see cref="IncompatibilityScope.AllPlatforms"/> の項目だけを拾う。
    /// <b>スキャナ側の判定ロジックには手を入れない。</b>
    /// SDK 判定（<c>FindIllegalComponents</c>）と自前リストのどちらを先に <c>reported</c> へ登録するかが
    /// ビルドターゲットで入れ替わる設計（DEC-075）になっており、判定を切り出すと Standalone で
    /// MeshCollider が「Quest では無効になる Collider（物理）… PC では動作します」と誤って説明される。
    /// </para>
    /// <para>
    /// 自動修正は用意しない。PC 版で意図して使っている可能性があり、消すと壊れるため（Issue #36）。
    /// </para>
    /// </summary>
    public sealed class UnsupportedComponentCheck : IAvatarCheck
    {
        /// <summary>内訳として常時表示する型の数。これを超えた分は「ほか N 種類」1行にまとめる。</summary>
        private const int VisibleTypeCount = 5;

        /// <inheritdoc/>
        public string DisplayName => "[SDK] 非対応コンポーネントチェック";

        /// <inheritdoc/>
        /// <remarks>VRChat SDK は asmdef の必須参照なので、常に実行できる。</remarks>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            return BuildResults(QuestCompatibilityScanner.Scan(avatarRoot));
        }

        /// <summary>
        /// スキャン結果を <see cref="CheckResult"/> 列に変換する本体。
        /// <see cref="QuestCompatibilityScanner.Scan(GameObject)"/> は実行中のエディタのアクティブ
        /// ビルドターゲットに依存するため、テストからスキャン結果を注入できるよう分離している
        /// （<c>QuestCompatibilityScannerTests</c> が <c>Scan(GameObject, bool)</c> を使うのと同じ理由）。
        /// </summary>
        internal static IEnumerable<CheckResult> BuildResults(IReadOnlyList<QuestIncompatibility> scanResults)
        {
            if (scanResults == null) yield break;

            var components = scanResults
                .Where(i => i.Scope == IncompatibilityScope.AllPlatforms)
                .SelectMany(i => i.Components)
                .Where(c => c != null)
                .ToList();

            if (components.Count == 0) yield break;

            // 個数の降順 → 型名の序数昇順。QuestCompatibilityScanner の内訳の並べ方に合わせる
            var groups = components
                .GroupBy(c => c.GetType().Name, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            // サマリー行。件数に数えるのはこの1行だけ（IsDetail = false）。
            // DynamicBone を 30 本入れたアバターで「30 Warning」になると、DEC-071 で直した
            // 件数の水増しが再発するため、1コンポーネント1行にはしない
            yield return new CheckResult(
                Severity.Warning,
                $"[SDK] VRChat が対応していないコンポーネントが {components.Count} 個あります"
                + $"（{groups.Count} 種類）。"
                + "アップロード時に取り除かれるため、入れたはずの機能がアバターで動きません。",
                hint: "PC 版でだけ使うものなら、そのままで問題ありません。",
                documentUrl: OmamoriDocUrls.AllowedAvatarComponents);

            // 内訳行。件数には数えず（IsDetail = true）、型ごとに1行だけ出す。
            // 「選択」で Ping できるのはその型の先頭1件のみ（CheckResult.TargetObject が単数のため。
            // 複数ターゲット対応は v0.10.0 のスコープ外として Issue に残す）
            foreach (var group in groups.Take(VisibleTypeCount))
            {
                yield return new CheckResult(
                    Severity.Warning,
                    $"{group.Key} ×{group.Count()}",
                    group.First().gameObject,
                    isDetail: true);
            }

            var hiddenTypeCount = groups.Count - VisibleTypeCount;
            if (hiddenTypeCount > 0)
            {
                yield return new CheckResult(
                    Severity.Warning,
                    $"ほか {hiddenTypeCount} 種類",
                    isDetail: true);
            }
        }
    }
}
