using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class ExpressionParameterBitLimitCheckTests
    {
        private GameObject _root;
        private VRCExpressionParameters _parameters;
        private readonly ExpressionParameterBitLimitCheck _check = new ExpressionParameterBitLimitCheck();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
            var descriptor = _root.AddComponent<VRCAvatarDescriptor>();
            _parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            descriptor.expressionParameters = _parameters;
        }

        [TearDown]
        public void TearDown()
        {
            if (_parameters != null) Object.DestroyImmediate(_parameters);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private static VRCExpressionParameters.Parameter Param(
            VRCExpressionParameters.ValueType type, bool synced, int index)
        {
            return new VRCExpressionParameters.Parameter
            {
                name = $"p{index}",
                valueType = type,
                networkSynced = synced,
            };
        }

        [Test]
        public void 上限ちょうど256bitは検出しない()
        {
            // Float は 8bit なので 32 個でちょうど 256bit
            _parameters.parameters = Enumerable.Range(0, 32)
                .Select(i => Param(VRCExpressionParameters.ValueType.Float, true, i))
                .ToArray();

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void 上限超過264bitをエラーとして検出する()
        {
            _parameters.parameters = Enumerable.Range(0, 33)
                .Select(i => Param(VRCExpressionParameters.ValueType.Float, true, i))
                .ToArray();

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Error, results[0].Severity);
            Assert.That(results[0].Message, Does.Contain("264 / 256"));
        }

        [Test]
        public void 非同期パラメータはビット数に数えない()
        {
            // Float 33個のうち1個を非同期にすると同期分は 256bit ちょうどに収まる
            _parameters.parameters = Enumerable.Range(0, 33)
                .Select(i => Param(VRCExpressionParameters.ValueType.Float, synced: i != 0, index: i))
                .ToArray();

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void Boolは1bitとして数える()
        {
            _parameters.parameters = Enumerable.Range(0, 257)
                .Select(i => Param(VRCExpressionParameters.ValueType.Bool, true, i))
                .ToArray();

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.That(results[0].Message, Does.Contain("257 / 256"));
        }
    }
}
