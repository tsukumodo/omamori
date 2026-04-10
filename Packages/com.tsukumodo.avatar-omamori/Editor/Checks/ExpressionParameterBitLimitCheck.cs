using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// VRC Expression Parameters の同期パラメータが VRChat の上限 (256 bit) を
    /// 超えていないかを検証する。超過するとアップロードに失敗する。
    /// </summary>
    public sealed class ExpressionParameterBitLimitCheck : IAvatarCheck
    {
        /// <summary>VRChat の同期パラメータ上限ビット数。</summary>
        private const int MaxSyncedBits = 256;

        /// <inheritdoc/>
        public string DisplayName => "[SDK] 同期パラメータ上限チェック";

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

            int totalCost = 0;
            foreach (var param in expressionParameters.parameters)
            {
                if (param == null)
                    continue;

                if (!param.networkSynced)
                    continue;

                totalCost += GetBitCost(param.valueType);
            }

            if (totalCost > MaxSyncedBits)
            {
                yield return new CheckResult(
                    Severity.Error,
                    $"[SDK] 同期パラメータのビット数が上限を超えています（{totalCost} / {MaxSyncedBits} bit、{totalCost - MaxSyncedBits} bit 超過）。アップロードに失敗します。不要な同期パラメータを削除するか、networkSynced を無効にしてください。",
                    expressionParameters
                );
            }
        }

        /// <summary>
        /// パラメータの ValueType に応じた同期ビットコストを返す。
        /// </summary>
        private static int GetBitCost(VRCExpressionParameters.ValueType valueType)
        {
            switch (valueType)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return 1;
                case VRCExpressionParameters.ValueType.Int:
                    return 8;
                case VRCExpressionParameters.ValueType.Float:
                    return 8;
                default:
                    // 現在のVRC SDK では Bool/Int/Float のみ。
                    // 新しい型が追加された場合はここを更新すること。
                    return 0;
            }
        }
    }
}
