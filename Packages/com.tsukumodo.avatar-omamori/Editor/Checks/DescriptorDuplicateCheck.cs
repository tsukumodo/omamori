using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバタールート以下に VRC_AvatarDescriptor が複数存在する場合にエラーを報告する。
    /// 複数の Descriptor が存在するとアバターのビルド・アップロードに失敗する。
    /// </summary>
    public sealed class DescriptorDuplicateCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "VRC Avatar Descriptor 重複チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var descriptors = avatarRoot.GetComponentsInChildren<VRCAvatarDescriptor>(true);

            if (descriptors.Length <= 1)
                yield break;

            yield return new CheckResult(
                Severity.Error,
                $"VRC Avatar Descriptor が {descriptors.Length} 個見つかりました。複数あるとアバターのビルド・アップロードに失敗します。アバタールートに1つだけ配置してください。",
                avatarRoot
            );

            // 各重複箇所も報告
            for (int i = 1; i < descriptors.Length; i++)
            {
                yield return new CheckResult(
                    Severity.Error,
                    $"重複した VRC Avatar Descriptor: {HierarchyPathUtil.GetHierarchyPath(descriptors[i].gameObject)}",
                    descriptors[i]
                );
            }
        }
    }
}
