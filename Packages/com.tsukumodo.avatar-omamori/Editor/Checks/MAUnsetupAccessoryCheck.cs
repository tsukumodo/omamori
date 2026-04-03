using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバター直下の子オブジェクトで Armature を持つのに
    /// MA Merge Armature も Bone Proxy も設定されていないものを警告する。
    /// 髪・衣装・アクセサリの「置いてけぼり」を汎用的に検出する。
    /// </summary>
    public sealed class MAUnsetupAccessoryCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[MA] 装飾物の未セットアップチェック";

        /// <inheritdoc/>
        public bool IsAvailable() => MAReflectionHelper.IsAvailable
                                     && MAReflectionHelper.MergeArmatureType != null
                                     && MAReflectionHelper.BoneProxyType != null;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var mergeArmatureType = MAReflectionHelper.MergeArmatureType;
            var boneProxyType = MAReflectionHelper.BoneProxyType;

            // アバター直下の子オブジェクトのみを走査
            foreach (Transform child in avatarRoot.transform)
            {
                var go = child.gameObject;

                // EditorOnlyタグ付きオブジェクトは除外
                if (go.CompareTag("EditorOnly"))
                    continue;

                // 子オブジェクト配下に Armature（名前が "Armature" の子Transform）があるか確認
                if (!HasArmatureChild(child))
                    continue;

                // MA Merge Armature または Bone Proxy が配下にあるか確認
                var hasMergeArmature = go.GetComponentsInChildren(mergeArmatureType, true).Length > 0;
                var hasBoneProxy = go.GetComponentsInChildren(boneProxyType, true).Length > 0;

                if (!hasMergeArmature && !hasBoneProxy)
                {
                    yield return new CheckResult(
                        Severity.Warning,
                        $"[MA] \"{go.name}\" は Armature を持っていますが、MA Merge Armature や Bone Proxy が設定されていません。衣装やアクセサリの場合、アバターに追従しない可能性があります。",
                        go
                    );
                }
            }
        }

        /// <summary>
        /// 指定した Transform の直下の子に "Armature" という名前の Transform があるかを確認する。
        /// </summary>
        private static bool HasArmatureChild(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name == "Armature")
                    return true;
            }
            return false;
        }
    }
}
