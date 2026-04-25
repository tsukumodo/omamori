using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEditor;
using UnityEditor.SceneManagement;
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

                    // ループ変数を直接ラムダに渡すとイテレーション共有で誤ったオブジェクトを参照する恐れがあるのでローカルにキャプチャする
                    var capturedGo = go;
                    var capturedCount = missingCount;

                    yield return new CheckResult(
                        Severity.Error,
                        $"[Unity] {path} に Missing Script が {missingCount} 個あります。「修正」ボタンで一括削除できます（Inspector の右クリック → Remove Component からも削除可能）。",
                        go,
                        fixAction: () => RemoveMissingScripts(capturedGo),
                        fixConfirmMessage:
                            $"「{capturedGo.name}」から Missing Script を {capturedCount} 個削除します。\n" +
                            "※この操作は Undo（Ctrl+Z）で元に戻せません。\n" +
                            "Missing Script はすでにスクリプト参照が壊れているコンポーネントなので、削除しても実質的なデータ損失はありません。"
                    );
                }
            }
        }

        /// <summary>
        /// 指定 GameObject から Missing Script を全て削除する。
        /// </summary>
        /// <remarks>
        /// Unity の仕様上、この操作は Undo（Ctrl+Z）で巻き戻せない：
        /// <list type="bullet">
        ///   <item><see cref="GameObjectUtility.RemoveMonoBehavioursWithMissingScript"/> は Undo 非対応。</item>
        ///   <item><see cref="SerializedObject"/> 経由で <c>m_Component</c> を直接編集する方式は、
        ///   Unity 側が保護データとして弾く（"It is not allowed to modify the data property"）。</item>
        /// </list>
        /// そのため、事前確認ダイアログで「Undo で戻せない」旨をユーザーに明示する。
        /// Missing Script は既に参照が壊れているコンポーネントなので、削除で失う情報は実質ない。
        /// </remarks>
        private static void RemoveMissingScripts(GameObject target)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            EditorUtility.SetDirty(target);

            // シーン上の GameObject の変更は SceneDirty を立てないと保存時に反映されないため明示的にマークする。
            // Prefab アセット上のオブジェクトは scene.IsValid() が false になるので SetDirty のみで十分。
            var scene = target.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
