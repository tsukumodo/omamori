using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバター配下のメッシュで Read/Write Enabled が無効になっているものを検出する。
    /// Read/Write が無効だと VRChat がポリゴン数を計測できず、
    /// PC・Quest ともにパフォーマンスランクが問答無用で Very Poor に落ちる。
    /// </summary>
    public sealed class MeshReadWriteCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[SDK] メッシュの Read/Write 無効チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            // 同じメッシュが複数の Renderer で共有されていても1回だけ報告する
            var reported = new HashSet<Mesh>();

            foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = GetMesh(renderer);
                if (mesh == null) continue;
                if (mesh.isReadable) continue;
                if (!reported.Add(mesh)) continue;

                var path = HierarchyPathUtil.GetHierarchyPath(renderer.gameObject);
                yield return new CheckResult(
                    Severity.Error,
                    $"[SDK] {path} のメッシュ \"{mesh.name}\" は Read/Write Enabled が無効です。"
                    + "この状態だとポリゴン数を計測できず、パフォーマンスランクが Very Poor になります。"
                    + "モデルの FBX を選択し、Model タブの Read/Write Enabled にチェックを入れてください。",
                    renderer.gameObject,
                    valueLabel: "Read/Write Enabled",
                    beforeValue: "無効"
                );
            }
        }

        /// <summary>
        /// Renderer が描画しているメッシュを返す。無ければ null。
        /// ⚠ <c>GetComponent&lt;MeshFilter&gt;()?.sharedMesh</c> は使わない。
        /// <c>?.</c> は C# の参照 null しか見ないため、破棄済みコンポーネント（Unity の疑似 null）を
        /// 「非 null」として通してしまう。<c>TryGetComponent</c> なら Unity 側の判定を通るうえ、
        /// 未取得時のアロケーションも避けられる。
        /// </summary>
        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            return renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
        }
    }
}
