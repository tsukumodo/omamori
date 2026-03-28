using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバター配下の全GameObjectを走査し、Missing状態のコンポーネントを検出する。
    /// </summary>
    public sealed class MissingScriptCheck : IAvatarCheck
    {
        public string DisplayName => "[Unity] Missing Script チェック";

        public bool IsAvailable() => true;

        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            // 非アクティブなGameObjectも含めて全ノードを取得
            var transforms = avatarRoot.GetComponentsInChildren<Transform>(true);

            foreach (var t in transforms)
            {
                var go = t.gameObject;
                int missingCount = GetMissingScriptCount(go);

                if (missingCount > 0)
                {
                    var path = GetHierarchyPath(go);
                    yield return new CheckResult(
                        Severity.Error,
                        $"[Unity] {path} に Missing Script が {missingCount} 個あります。不要なMissing Scriptは右クリック → Remove Componentで削除してください。",
                        go
                    );
                }
            }
        }

        private static int GetMissingScriptCount(GameObject go)
        {
#if UNITY_EDITOR
            // Unity 2022.3 で利用可能な GameObjectUtility API を使用
            return GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
#else
            // フォールバック: component == null パターン
            var components = go.GetComponents<Component>();
            int count = 0;
            foreach (var c in components)
            {
                if (c == null)
                    count++;
            }
            return count;
#endif
        }

        private static string GetHierarchyPath(GameObject obj)
        {
            var path = obj.name;
            var current = obj.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
