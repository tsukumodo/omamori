using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// <see cref="IAvatarCheck"/> の実装をリフレクションで自動検出し、一括実行する。
    /// </summary>
    public static class CheckRunner
    {
        private static List<IAvatarCheck> s_checks;

        /// <summary>
        /// 検出された全チェックの一覧。初回アクセス時にリフレクションで検出される。
        /// </summary>
        public static IReadOnlyList<IAvatarCheck> Checks
        {
            get
            {
                if (s_checks == null)
                    s_checks = DiscoverChecks();
                return s_checks;
            }
        }

        /// <summary>
        /// 全ての利用可能なチェックをアバタールートに対して実行し、結果を返す（既存シグネチャ・互換維持）。
        /// このメソッドは利用統計を書き込まない。記録は呼び出し側が <see cref="UsageStatsRecorder.RecordRun"/>
        /// で1回だけ行う（Issue #35）。
        /// </summary>
        /// <param name="avatarRoot">チェック対象のアバタールート GameObject。</param>
        /// <returns>全チェックの結果を集約したリスト。</returns>
        public static List<CheckResult> RunAll(GameObject avatarRoot)
        {
            return RunAll(avatarRoot, Checks, out _);
        }

        /// <summary>
        /// 全ての利用可能なチェックをアバタールートに対して実行し、結果と検出件数を返す。
        /// このメソッドは利用統計を書き込まない。記録は呼び出し側が <see cref="UsageStatsRecorder.RecordRun"/>
        /// で1回だけ行う（Issue #35）。
        /// </summary>
        /// <param name="avatarRoot">チェック対象のアバタールート GameObject。</param>
        /// <param name="detectionsByCheckType">チェッククラス名 → 今回の検出件数。呼び出し側の記録に使う。</param>
        /// <returns>全チェックの結果を集約したリスト。</returns>
        public static List<CheckResult> RunAll(GameObject avatarRoot, out IReadOnlyDictionary<string, int> detectionsByCheckType)
        {
            return RunAll(avatarRoot, Checks, out detectionsByCheckType);
        }

        /// <summary>
        /// 指定されたチェック群を実行する（既存 internal・互換維持）。
        /// テストからフェイクチェックを注入して例外分離などを検証できるよう分離している。
        /// このメソッドは利用統計を書き込まない。記録は呼び出し側が <see cref="UsageStatsRecorder.RecordRun"/>
        /// で1回だけ行う（Issue #35）。
        /// </summary>
        internal static List<CheckResult> RunAll(GameObject avatarRoot, IEnumerable<IAvatarCheck> checks)
        {
            return RunAll(avatarRoot, checks, out _);
        }

        /// <summary>
        /// 指定されたチェック群を実行する本体。
        /// テストからフェイクチェックを注入して例外分離などを検証できるよう分離している。
        ///
        /// <para>
        /// このメソッドは利用統計を書き込まない。記録は呼び出し側が <see cref="UsageStatsRecorder.RecordRun"/>
        /// で1回だけ行う（Issue #35）。以前はここで <see cref="UsageStatsRecorder.RecordCheckRun"/> を直接呼んでいたが、
        /// <see cref="AvatarOmamoriWindow.RunChecks"/> 側のパフォーマンス集計と合わせて1回の実行で2回ディスクに
        /// 書き込まれてしまっていたため、書き込み責務を呼び出し側の1箇所に一本化した。
        /// </para>
        /// </summary>
        internal static List<CheckResult> RunAll(GameObject avatarRoot, IEnumerable<IAvatarCheck> checks, out IReadOnlyDictionary<string, int> detectionsByCheckType)
        {
            var results = new List<CheckResult>();
            // チェック種別ごとの検出件数（利用統計用）。キーはチェッククラスの型名のみを使い、
            // ユーザーのアセット名などは一切載せない（個人情報を集めない設計・DEC-055）。
            var detections = new Dictionary<string, int>();

            foreach (var check in checks)
            {
                if (!check.IsAvailable())
                    continue;

                try
                {
                    // yield 実装の二重実行を避けるため、ここで1度だけ列挙する
                    var checkResults = check.Execute(avatarRoot).ToList();

                    // IsDetail は「直前のサマリー行の内訳」という契約。サマリー行なしの内訳は UI に出さない（ShouldDrawGroup）ため、
                    // 黙って情報が落ちないよう開発時に気づける形で警告する（DEC-070）
                    if (checkResults.Count > 0 && checkResults[0].IsDetail)
                        Debug.LogWarning($"[AvatarOmamori] Check '{check.DisplayName}' returned a detail row without a summary row.");

                    results.AddRange(checkResults);
                    if (checkResults.Count > 0)
                    {
                        var key = check.GetType().Name;
                        detections.TryGetValue(key, out int cur);
                        detections[key] = cur + checkResults.Count;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AvatarOmamori] Check '{check.DisplayName}' threw an exception: {e}");
                }
            }

            detectionsByCheckType = detections;
            return results;
        }

        /// <summary>
        /// 同一アセンブリ内から <see cref="IAvatarCheck"/> の実装クラスをリフレクションで検出する。
        /// </summary>
        private static List<IAvatarCheck> DiscoverChecks()
        {
            var checkType = typeof(IAvatarCheck);
            return checkType.Assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && checkType.IsAssignableFrom(t))
                .Select(t =>
                {
                    try { return (IAvatarCheck)Activator.CreateInstance(t); }
                    catch { return null; }
                })
                .Where(c => c != null)
                .ToList();
        }
    }
}
