using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// VRC Expression Parameters の問題を検出する。
    /// - Expression Menu が設定されているのに Expression Parameters が未設定の場合を警告
    /// - Parameter 配列に名前が空文字のエントリがある場合を警告
    /// </summary>
    public sealed class EmptyParameterNameCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "[SDK] Expression Parameter チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
                yield break;

            var expressionsMenu = descriptor.expressionsMenu;
            var expressionParameters = descriptor.expressionParameters;

            // Expression Menu が設定されているのに Parameters が未設定
            if (expressionsMenu != null && expressionParameters == null)
            {
                yield return new CheckResult(
                    Severity.Warning,
                    "[SDK] Expression Menu が設定されていますが、Expression Parameters が未設定です。メニューのトグルやボタンが正しく動作しません。",
                    descriptor
                );
                yield break;
            }

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
