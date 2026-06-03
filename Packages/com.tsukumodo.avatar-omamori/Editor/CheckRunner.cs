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
        /// 全ての利用可能なチェックをアバタールートに対して実行し、結果を返す。
        /// 個別チェックで例外が発生した場合はログに警告を出力し、残りのチェックを続行する。
        /// </summary>
        /// <param name="avatarRoot">チェック対象のアバタールート GameObject。</param>
        /// <returns>全チェックの結果を集約したリスト。</returns>
        public static List<CheckResult> RunAll(GameObject avatarRoot)
        {
            var results = new List<CheckResult>();
            // チェック種別ごとの検出件数（利用統計用）。キーはチェッククラスの型名のみを使い、
            // ユーザーのアセット名などは一切載せない（個人情報を集めない設計・DEC-055）。
            var detectionsByCheckType = new Dictionary<string, int>();

            foreach (var check in Checks)
            {
                if (!check.IsAvailable())
                    continue;

                try
                {
                    // yield 実装の二重実行を避けるため、ここで1度だけ列挙する
                    var checkResults = check.Execute(avatarRoot).ToList();
                    results.AddRange(checkResults);
                    if (checkResults.Count > 0)
                    {
                        var key = check.GetType().Name;
                        detectionsByCheckType.TryGetValue(key, out int cur);
                        detectionsByCheckType[key] = cur + checkResults.Count;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AvatarOmamori] Check '{check.DisplayName}' threw an exception: {e}");
                }
            }

            // 利用統計に「チェック実行1回＋種別ごとの検出件数」を記録する（opt-out 中は内部で何もしない）
            UsageStatsRecorder.RecordCheckRun(detectionsByCheckType);

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
