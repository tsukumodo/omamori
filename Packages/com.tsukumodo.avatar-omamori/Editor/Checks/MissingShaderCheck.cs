using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバター配下の全 Renderer のマテリアルを走査し、
    /// シェーダーが見つからない（ピンク表示になる）ものを検出する。
    /// シェーダー未インポート時にアバターがピンク色で表示される問題を事前に防止する。
    /// </summary>
    public sealed class MissingShaderCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[Shader] シェーダー未検出（ピンクマテリアル）チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            // 非アクティブなGameObjectも含めて全Rendererを取得
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];

                    // マテリアルがnullの場合も検出対象
                    if (mat == null)
                    {
                        var path = HierarchyPathUtil.GetHierarchyPath(renderer.gameObject);
                        yield return new CheckResult(
                            Severity.Warning,
                            $"[Shader] {path} のマテリアルスロット [{i}] が null です。その部分が正しく表示されません。",
                            renderer.gameObject,
                            hint: "Renderer の Materials に、その部分のマテリアルを割り当てられます。"
                        );
                        continue;
                    }

                    // シェーダーがnull、またはエラーシェーダーの場合を検出
                    if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        var path = HierarchyPathUtil.GetHierarchyPath(renderer.gameObject);
                        var shaderInfo = mat.shader == null ? "null" : mat.shader.name;
                        yield return new CheckResult(
                            Severity.Warning,
                            $"[Shader] {path} のマテリアル \"{mat.name}\" (スロット [{i}]) のシェーダーが見つかりません ({shaderInfo})。ピンク色で表示されます。",
                            renderer.gameObject,
                            hint: "lilToon など対象のシェーダーをプロジェクトにインポートすると直ります。"
                        );
                    }
                }
            }
        }
    }
}
