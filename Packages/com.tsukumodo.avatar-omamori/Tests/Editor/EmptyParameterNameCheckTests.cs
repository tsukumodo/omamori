using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class EmptyParameterNameCheckTests
    {
        private GameObject _root;
        private VRCAvatarDescriptor _descriptor;
        private VRCExpressionsMenu _menu;
        private VRCExpressionParameters _parameters;
        private readonly EmptyParameterNameCheck _check = new EmptyParameterNameCheck();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
            _descriptor = _root.AddComponent<VRCAvatarDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_menu != null) Object.DestroyImmediate(_menu);
            if (_parameters != null) Object.DestroyImmediate(_parameters);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private static VRCExpressionParameters.Parameter Param(string name)
        {
            return new VRCExpressionParameters.Parameter
            {
                name = name,
                valueType = VRCExpressionParameters.ValueType.Bool,
                networkSynced = true,
            };
        }

        [Test]
        public void Descriptorがなければ検出しない()
        {
            var plain = new GameObject("NoDescriptor");
            try
            {
                Assert.IsEmpty(_check.Execute(plain).ToList());
            }
            finally
            {
                Object.DestroyImmediate(plain);
            }
        }

        [Test]
        public void MenuがあるのにParametersが未設定なら警告する()
        {
            _menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            _descriptor.expressionsMenu = _menu;
            _descriptor.expressionParameters = null;

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Warning, results[0].Severity);
            Assert.That(results[0].Message, Does.Contain("Expression Parameters が未設定"));
        }

        [Test]
        public void 名前が空のエントリを添字付きで警告する()
        {
            _parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            _parameters.parameters = new[] { Param("Valid"), Param("") };
            _descriptor.expressionParameters = _parameters;

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Warning, results[0].Severity);
            Assert.That(results[0].Message, Does.Contain("[1]"));
        }

        [Test]
        public void 全エントリに名前があれば検出しない()
        {
            _parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            _parameters.parameters = new[] { Param("A"), Param("B") };
            _descriptor.expressionParameters = _parameters;

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }
    }
}
