using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    public static class CheckRunner
    {
        private static List<IAvatarCheck> s_checks;

        public static IReadOnlyList<IAvatarCheck> Checks
        {
            get
            {
                if (s_checks == null)
                    s_checks = DiscoverChecks();
                return s_checks;
            }
        }

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
