using System.Collections;
using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// MA ObjectToggle の m_objects 内のターゲットが空、または自身の GameObject を参照している場合にエラーを報告する。
    /// 空ターゲットは意図しない動作の原因になり、自己参照は MA ビルド時にエラー（MA-1200）を引き起こす。
    /// </summary>
    public sealed class MAObjectToggleSelfRefCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "MA ObjectToggle 自己参照チェック";

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
