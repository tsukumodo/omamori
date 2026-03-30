using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバター配下の全 GameObject を走査し、Missing 状態のコンポーネントを検出する。
    /// Missing Script はビルドエラーや予期しない動作の原因となる。
    /// </summary>
    public sealed class MissingScriptCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[Unity] Missing Script チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            // 非アクティブなGameObjectも含めて全ノードを取得
            var transforms = avatarRoot.GetComponentsInChildren<Transform>(true);

            foreach (var t in transforms)
            {
                var go = t.gameObject;
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

                if (missingCount > 0)
                {
                    var path = HierarchyPathUtil.GetHierarchyPath(go);
                    yield return new CheckResult(
                        Severity.Error,
                        $"[Unity] {path} に Missing Script が {missingCount} 個あります。不要なMissing Scriptは右クリック → Remove Componentで削除してください。",
                        go
                    );
                }
            }
        }
    }
}
