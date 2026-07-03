using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class AnimatorLayerWeightCheckTests
    {
        private string _statsDir;
        private GameObject _root;
        private AnimatorController _controller;
        private readonly AnimatorLayerWeightCheck _check = new AnimatorLayerWeightCheck();

        [SetUp]
        public void SetUp()
        {
            // FixAction が利用統計を記録するため、保存先を一時フォルダへ退避する
            _statsDir = UsageStatsTestUtil.BeginOverride();
            FixHistoryStore.Clear();
            _root = new GameObject("Avatar");
        }

        [TearDown]
        public void TearDown()
        {
            UsageStatsTestUtil.EndOverride(_statsDir);
            FixHistoryStore.Clear();
            if (_controller != null) Object.DestroyImmediate(_controller);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 指定した weight 配列のレイヤーを持つ FX コントローラーを組み立てて Descriptor に設定する。
        /// </summary>
        private void SetupFxLayer(bool isDefault, params float[] weights)
        {
            _controller = new AnimatorController();
            for (int i = 0; i < weights.Length; i++)
            {
                _controller.AddLayer($"Layer{i}");
            }
            var layers = _controller.layers; // getter はコピーを返すため、変更してからセットし直す
            for (int i = 0; i < weights.Length; i++)
            {
                layers[i].defaultWeight = weights[i];
            }
            _controller.layers = layers;

            var descriptor = _root.AddComponent<VRCAvatarDescriptor>();
            descriptor.baseAnimationLayers = new[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    isDefault = isDefault,
                    animatorController = _controller,
                },
            };
        }

        [Test]
        public void Weight0のFXレイヤーを警告し修正で1になる()
        {
            SetupFxLayer(isDefault: false, 1f, 0f);

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            var result = results[0];
            Assert.AreEqual(Severity.Warning, result.Severity);
            Assert.That(result.Message, Does.Contain("Layer1"));
            Assert.IsTrue(result.HasFix);
            Assert.AreEqual("0", result.BeforeValue);
            Assert.AreEqual("1", result.AfterValue);

            result.FixAction();

            Assert.AreEqual(1f, _controller.layers[1].defaultWeight, "修正後も Weight が 0 のまま");
            Assert.IsEmpty(_check.Execute(_root).ToList());
            Assert.AreEqual(1, FixHistoryStore.Count, "修正履歴が記録されていない");
        }

        [Test]
        public void ベースレイヤーのWeight0は検出しない()
        {
            // index 0 は常に Weight=1 で動作する仕様のためチェック対象外
            SetupFxLayer(isDefault: false, 0f, 1f);

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void isDefaultのFXレイヤーは検出しない()
        {
            SetupFxLayer(isDefault: true, 1f, 0f);

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }
    }
}
