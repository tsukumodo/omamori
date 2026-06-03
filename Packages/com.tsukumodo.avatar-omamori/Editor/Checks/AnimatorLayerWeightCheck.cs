using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// FX Animator Controller のレイヤーで Weight が 0 のものを検出する。
    /// Weight=0 のレイヤーはアニメーションが反映されないため、
    /// エクスプレッション・ギミックが動作しない最も一般的な原因となる。
    /// </summary>
    public sealed class AnimatorLayerWeightCheck : IAvatarCheck
    {
        // CheckResult の予告表示と FixHistoryEntry の実測記録で同じ文字列を共有するため定数化
        private const string ValueLabel = "FX Layer / Weight";

        /// <inheritdoc/>
        public string DisplayName => "[SDK] Animator Layer Weight チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
                yield break;

            // baseAnimationLayers から FX レイヤーを探す
            var fxLayerEntry = FindFxLayer(descriptor);
            if (fxLayerEntry == null)
                yield break;

            var customLayer = fxLayerEntry.Value;

            // デフォルト（カスタムコントローラー未設定）ならチェック不要
            if (customLayer.isDefault)
                yield break;

            if (customLayer.animatorController == null)
                yield break;

            var animatorController = customLayer.animatorController as AnimatorController;
            if (animatorController == null)
                yield break;

            var layers = animatorController.layers;
            if (layers == null)
                yield break;

            // index 0（ベースレイヤー）はスキップ。ベースレイヤーは常に Weight=1 で動作する
            for (int i = 1; i < layers.Length; i++)
            {
                if (layers[i].defaultWeight == 0f)
                {
                    // クロージャでキャプチャするため、ループ変数をローカルコピーに取る
                    var capturedController = animatorController;
                    var capturedIndex = i;
                    var capturedName = layers[i].name;

                    yield return new CheckResult(
                        Severity.Warning,
                        $"[SDK] FX レイヤー \"{layers[i].name}\" (index {i}) の Weight が 0 です。このレイヤーのアニメーションは反映されません。Weight を 1 に変更してください。",
                        descriptor,
                        fixAction: () => SetLayerWeightToOne(capturedController, capturedIndex, capturedName),
                        fixConfirmMessage: $"FXレイヤー「{capturedName}」のWeightを 0 → 1 に変更します。\nUndo（Ctrl+Z）で元に戻せます。",
                        valueLabel: ValueLabel,
                        beforeValue: "0",
                        afterValue: "1"
                    );
                }
            }
        }

        /// <summary>
        /// 指定レイヤーの defaultWeight を 1 に設定する。
        /// AnimatorController.layers の getter はコピーを返すため、ローカル変数で変更してからセットし直す。
        /// 修正直前/直後に実値を読んで <see cref="FixHistoryStore"/> に記録する（DEC-055 設計選択C）。
        /// </summary>
        private static void SetLayerWeightToOne(AnimatorController controller, int layerIndex, string layerName)
        {
            float before = controller.layers[layerIndex].defaultWeight;

            Undo.RecordObject(controller, "Fix Animator Layer Weight");
            var layers = controller.layers;
            layers[layerIndex].defaultWeight = 1f;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            float after = controller.layers[layerIndex].defaultWeight;

            FixHistoryStore.Record(new FixHistoryEntry(
                timestamp: System.DateTime.Now,
                checkName: nameof(AnimatorLayerWeightCheck),
                valueLabel: ValueLabel,
                beforeValue: before.ToString(),
                afterValue: after.ToString(),
                targetInstanceID: controller.GetInstanceID(),
                targetObjectName: $"{controller.name} / \"{layerName}\" (Layer {layerIndex})"
            ));

            // 利用統計に修正実行を記録（種別名のみ・opt-out 中は内部で何もしない・DEC-055）
            UsageStatsRecorder.RecordFix(nameof(AnimatorLayerWeightCheck));
        }

        /// <summary>
        /// baseAnimationLayers から FX レイヤーのエントリを探す。
        /// </summary>
        private static VRCAvatarDescriptor.CustomAnimLayer? FindFxLayer(VRCAvatarDescriptor descriptor)
        {
            if (descriptor.baseAnimationLayers == null)
                return null;

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer;
            }

            return null;
        }
    }
}
