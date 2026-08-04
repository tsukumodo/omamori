using System.Linq;
using AvatarOmamori.Editor.Performance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// Quest / iOS 非対応要素の検出（層A）のテスト。
    ///
    /// シェーダー判定は SDK（<c>AvatarValidation.FindIllegalShaders</c>）に委譲しているため、
    /// ここでは自前リストで判定しているコンポーネント側を固定する。
    /// </summary>
    public class QuestCompatibilityScannerTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
            var child = new GameObject("Child");
            child.transform.SetParent(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private GameObject Child => _root.transform.GetChild(0).gameObject;

        private static bool HasLabelContaining(System.Collections.Generic.List<QuestIncompatibility> results, string keyword)
        {
            return results.Any(r => r.Label.Contains(keyword));
        }

        [Test]
        public void 禁止コンポーネントが無ければコンポーネント系の検出は出ない()
        {
            var results = QuestCompatibilityScanner.Scan(_root);

            Assert.IsFalse(HasLabelContaining(results, "Light"));
            Assert.IsFalse(HasLabelContaining(results, "Audio Source"));
            Assert.IsFalse(HasLabelContaining(results, "Constraint"));
        }

        [Test]
        public void Lightを検出する()
        {
            Child.AddComponent<Light>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var light = results.FirstOrDefault(r => r.Label.Contains("Light"));
            Assert.IsNotNull(light);
            Assert.AreEqual(1, light.Count);
            CollectionAssert.Contains(light.Targets, Child);
        }

        [Test]
        public void AudioSourceを検出する()
        {
            Child.AddComponent<AudioSource>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var audio = results.FirstOrDefault(r => r.Label.Contains("Audio Source"));
            Assert.IsNotNull(audio);
            Assert.AreEqual(1, audio.Count);
        }

        [Test]
        public void 非アクティブなオブジェクトの禁止コンポーネントも検出する()
        {
            Child.AddComponent<Light>();
            Child.SetActive(false);

            var results = QuestCompatibilityScanner.Scan(_root);

            Assert.IsTrue(HasLabelContaining(results, "Light"));
        }

        [Test]
        public void UnityのConstraintを別項目として検出する()
        {
            Child.AddComponent<ParentConstraint>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var constraint = results.FirstOrDefault(r => r.Label.Contains("Unity の Constraint"));
            Assert.IsNotNull(constraint);
            Assert.AreEqual(1, constraint.Count);
            StringAssert.Contains("VRChat Constraints", constraint.Detail);
        }

        [Test]
        public void 複数の禁止コンポーネントはそれぞれ別の項目になる()
        {
            Child.AddComponent<Light>();
            Child.AddComponent<AudioSource>();

            var results = QuestCompatibilityScanner.Scan(_root);

            Assert.IsTrue(HasLabelContaining(results, "Light"));
            Assert.IsTrue(HasLabelContaining(results, "Audio Source"));
        }
    }
}
