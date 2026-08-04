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

        [Test]
        public void Quest固有の項目はQuestスコープになる()
        {
            Child.AddComponent<Light>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var light = results.First(r => r.Label.Contains("Light"));
            Assert.AreEqual(IncompatibilityScope.Quest, light.Scope);
        }

        [Test]
        public void 同じコンポーネントが複数の項目で二重に数えられない()
        {
            // MeshCollider は自前リストの Collider（物理）にも、SDK の非対応判定にも引っかかる。
            // 同じ問題が2行に出ると件数が信用されなくなるため、必ずどちらか一方に寄せる
            Child.AddComponent<MeshCollider>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var reportingChild = results.Where(r => r.Targets.Contains(Child)).ToList();
            Assert.AreEqual(
                1, reportingChild.Count,
                "同じオブジェクトが複数の項目から報告されています: "
                + string.Join(" / ", reportingChild.Select(r => r.Label)));
        }

        [Test]
        public void PCでも剥がされるコンポーネントはSDK判定側に寄せる()
        {
            // MeshCollider は PC でも取り除かれるので、自前リストの
            // 「Quest では無効になる Collider（物理）… PC では動作します」に寄せると説明が嘘になる。
            // SDK 判定のほうが現在のビルドターゲットに対して正確なので、そちらを残す
            Child.AddComponent<MeshCollider>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var row = results.Single(r => r.Targets.Contains(Child));
            Assert.AreEqual(IncompatibilityScope.AllPlatforms, row.Scope);
            StringAssert.DoesNotContain("PC では動作します", row.Detail);
        }

        [Test]
        public void PCで許容されるColliderはQuest側の項目に残る()
        {
            // BoxCollider は PC では動作するので、自前リスト側の説明で正しい
            Child.AddComponent<BoxCollider>();

            var results = QuestCompatibilityScanner.Scan(_root);

            var row = results.Single(r => r.Targets.Contains(Child));
            Assert.AreEqual(IncompatibilityScope.Quest, row.Scope);
            StringAssert.Contains("Collider", row.Label);
        }

        [Test]
        public void PC共通の項目にはQuestドキュメントを案内しない()
        {
            // 「Quest では〜」以外の項目で Quest のドキュメントを開かせると、
            // PC しか使わないユーザーが「自分には関係ない」と誤解する
            Child.AddComponent<Light>();

            var results = QuestCompatibilityScanner.Scan(_root);

            foreach (var item in results.Where(r => r.Scope == IncompatibilityScope.AllPlatforms))
            {
                Assert.IsNull(item.DocumentUrl, $"{item.Label} に Quest のドキュメントが割り当てられています");
            }
        }
    }
}
