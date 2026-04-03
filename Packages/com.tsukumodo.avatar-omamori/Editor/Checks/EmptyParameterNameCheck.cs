using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// VRC Expression Parameters の Parameter 配列に名前が空文字のエントリがある場合を検出する。
    /// 空パラメータは同期帯域を無駄に消費し、意図しない動作の原因になる。
    /// </summary>
    public sealed class EmptyParameterNameCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[SDK] Expression Parameter 空欄チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
                yield break;

            var expressionParameters = descriptor.expressionParameters;
            if (expressionParameters == null || expressionParameters.parameters == null)
                yield break;

            for (int i = 0; i < expressionParameters.parameters.Length; i++)
            {
                var param = expressionParameters.parameters[i];
                if (param == null)
                    continue;

                if (string.IsNullOrEmpty(param.name))
                {
                    yield return new CheckResult(
                        Severity.Warning,
                        $"[SDK] Expression Parameters のエントリ [{i}] の名前が空です。不要なパラメータは削除してください。同期帯域を無駄に消費します。",
                        expressionParameters
                    );
                }
            }
        }
    }
}
