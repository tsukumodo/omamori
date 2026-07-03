using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class MissingShaderCheckTests
    {
        private GameObject _root;
        private Material _material;
        private readonly MissingShaderCheck _check = new MissingShaderCheck();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null) Object.DestroyImmediate(_material);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Renderer BodyRenderer => _root.GetComponentInChildren<Renderer>();

        [Test]
        public void 正常なマテリアルなら検出しない()
        {
            _material = new Material(Shader.Find("Standard"));
            BodyRenderer.sharedMaterials = new[] { _material };

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void nullマテリアルスロットを検出する()
        {
            BodyRenderer.sharedMaterials = new Material[] { null };

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Warning, results[0].Severity);
            Assert.That(results[0].Message, Does.Contain("[0]"));
        }

        [Test]
        public void エラーシェーダーのマテリアルを検出する()
        {
            _material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            BodyRenderer.sharedMaterials = new[] { _material };

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Warning, results[0].Severity);
            Assert.That(results[0].Message, Does.Contain("Hidden/InternalErrorShader"));
        }
    }
}
