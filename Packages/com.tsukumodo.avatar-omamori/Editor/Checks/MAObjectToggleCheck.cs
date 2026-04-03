using System.Collections;
using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// MA ObjectToggle の設定バリデーション。
    /// 空ターゲットや自己参照など、ObjectToggle に関する問題を検出する。
    /// </summary>
    public sealed class MAObjectToggleCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[MA] ObjectToggle チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => MAReflectionHelper.IsAvailable
                                     && MAReflectionHelper.ObjectToggleType != null;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var toggleType = MAReflectionHelper.ObjectToggleType;
            var toggleComponents = avatarRoot.GetComponentsInChildren(toggleType, true);

            foreach (var toggle in toggleComponents)
            {
                IList objects = MAReflectionHelper.GetToggleObjects(toggle);
                if (objects == null || objects.Count == 0)
                    continue;

                for (int i = 0; i < objects.Count; i++)
                {
                    var entry = objects[i];
                    if (entry == null) continue;

                    var targetGo = MAReflectionHelper.ResolveToggledTarget(entry, toggle);
                    if (targetGo == null)
                    {
                        yield return new CheckResult(
                            Severity.Warning,
                            $"[MA] ObjectToggle \"{toggle.gameObject.name}\" のトグルリスト [{i}] のターゲットが空です。不要なエントリは削除してください。",
                            toggle
                        );
                    }
                    else if (targetGo == toggle.gameObject)
                    {
                        yield return new CheckResult(
                            Severity.Error,
                            $"[MA] ObjectToggle \"{toggle.gameObject.name}\" のトグルリスト [{i}] が自身の GameObject を参照しています。MAビルドエラー（MA-1200）の原因になります。",
                            toggle
                        );
                    }
                }
            }
        }
    }
}
