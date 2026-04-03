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
            foreach (var check in Checks)
            {
                if (!check.IsAvailable())
                    continue;

                try
                {
                    results.AddRange(check.Execute(avatarRoot));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AvatarOmamori] Check '{check.DisplayName}' threw an exception: {e}");
                }
            }
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
