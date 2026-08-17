using System.Collections.Generic;
using System.Linq;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;
using AvatarOmamori.Editor.Performance;
using NUnit.Framework;
using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// VRChat 非対応コンポーネント検出（Issue #36）のテスト。
    ///
    /// <para>
    /// 判定そのものは <see cref="QuestCompatibilityScanner"/>（＝SDK の <c>FindIllegalComponents</c>）の
    /// 担当で、<c>QuestCompatibilityScannerTests</c> で固定済み。ここで固定するのは
    /// <b>スキャン結果を CheckResult に変換する部分</b>（サマリー＋型別内訳・件数の数え方・並び順）。
    /// そのため大半のケースはスキャン結果を組み立てて <c>BuildResults</c> に直接渡し、
    /// SDK とアクティブビルドターゲットに依存しないようにしている。
    /// </para>
    /// </summary>
    public class UnsupportedComponentCheckTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        /// <summary>子オブジェクトに <typeparamref name="T"/> を付けて返す。</summary>
        private T AddChild<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            return go.AddComponent<T>();
        }

        /// <summary>AllPlatforms スコープのスキャン結果を1件組み立てる。</summary>
        private static List<QuestIncompatibility> AllPlatformsScan(params Component[] components)
        {
            return new List<QuestIncompatibility>
            {
                new QuestIncompatibility(
                    "VRChat が対応していないコンポーネント",
                    components.Length,
                    "（テスト）",
                    components.Select(c => (Object)c.gameObject).Distinct().ToList(),
                    IncompatibilityScope.AllPlatforms,
                    components: components)
            };
        }

        private static List<CheckResult> Run(List<QuestIncompatibility> scan)
        {
            return UnsupportedComponentCheck.BuildResults(scan).ToList();
        }

        [Test]
        public void 非対応コンポーネントが無ければ何も報告しない()
        {
            Assert.IsEmpty(Run(new List<QuestIncompatibility>()));
        }

        [Test]
        public void Questスコープの項目は拾わない()
        {
            // 「Quest では使えないもの」はパフォーマンスセクションの担当、
            // こちらでも拾うと同じ検出が2箇所に出て件数が水増しされる（DEC-071）
            var light = AddChild<Light>("Light");
            var scan = new List<QuestIncompatibility>
            {
                new QuestIncompatibility(
                    "Quest では無効になる Light", 1, "（テスト）",
                    new List<Object> { light.gameObject },
                    IncompatibilityScope.Quest,
                    components: new Component[] { light })
            };

            Assert.IsEmpty(Run(scan));
        }

        [Test]
        public void 件数に数えるのはサマリーの1行だけ()
        {
            // 1コンポーネント1行にすると、DynamicBone を 30 本入れたアバターで「30 Warning」になり、
            // DEC-071 で直した件数の水増しが再発する
            var results = Run(AllPlatformsScan(
                AddChild<MeshCollider>("A"), AddChild<MeshCollider>("B"), AddChild<MeshCollider>("C"),
                AddChild<Light>("D")));

            Assert.AreEqual(1, results.Count(r => !r.IsDetail), "サマリー行は1行だけであるべき");
            Assert.AreEqual(2, results.Count(r => r.IsDetail), "内訳行は型ごとに1行であるべき");
        }

        [Test]
        public void サマリーに合計個数と型の種類数を書く()
        {
            var summary = Run(AllPlatformsScan(
                AddChild<MeshCollider>("A"), AddChild<MeshCollider>("B"), AddChild<MeshCollider>("C"),
                AddChild<Light>("D"))).First(r => !r.IsDetail);

            StringAssert.Contains("4 個", summary.Message);
            StringAssert.Contains("2 種類", summary.Message);
        }

        [Test]
        public void サマリー行には選択対象を持たせない()
        {
            // 対象は型ごとの内訳行が持つ。サマリーに代表1件をぶら下げると
            // 「なぜこの1件だけが選ばれるのか」を説明できない
            var summary = Run(AllPlatformsScan(AddChild<MeshCollider>("A"))).First(r => !r.IsDetail);

            Assert.IsNull(summary.TargetObject);
        }

        [Test]
        public void 内訳は個数の降順で型ごとに1行ずつ出す()
        {
            var results = Run(AllPlatformsScan(
                AddChild<Light>("A"),
                AddChild<MeshCollider>("B"), AddChild<MeshCollider>("C"), AddChild<MeshCollider>("D"),
                AddChild<BoxCollider>("E"), AddChild<BoxCollider>("F")));

            CollectionAssert.AreEqual(
                new[] { "MeshCollider ×3", "BoxCollider ×2", "Light ×1" },
                results.Where(r => r.IsDetail).Select(r => r.Message).ToList());
        }

        [Test]
        public void 内訳行はその型の先頭1件を選択対象にする()
        {
            var first = AddChild<MeshCollider>("First");
            var second = AddChild<MeshCollider>("Second");

            var detail = Run(AllPlatformsScan(first, second)).Single(r => r.IsDetail);

            Assert.AreSame(first.gameObject, detail.TargetObject);
        }

        [Test]
        public void 型が6種類以上あるときは上位5型とほかN種類にまとめる()
        {
            // 縦に伸びて 493×430 のウィンドウから溢れるのを防ぐ。サマリーの件数には影響させない。
            // 同数の型は型名の序数昇順（BoxCollider → Camera → CapsuleCollider → Light → Rigidbody）
            var results = Run(AllPlatformsScan(
                AddChild<MeshCollider>("A"), AddChild<MeshCollider>("B"),
                AddChild<BoxCollider>("C"),
                AddChild<Camera>("D"),
                AddChild<CapsuleCollider>("E"),
                AddChild<Light>("F"),
                AddChild<Rigidbody>("G")));

            var details = results.Where(r => r.IsDetail).ToList();

            Assert.AreEqual(6, details.Count, "上位5型 +「ほか N 種類」の1行になるべき");
            Assert.AreEqual("MeshCollider ×2", details[0].Message);
            Assert.AreEqual("ほか 1 種類", details.Last().Message);
            StringAssert.Contains("6 種類", results.First(r => !r.IsDetail).Message);
        }

        [Test]
        public void まとめ行は選択対象を持たない()
        {
            var results = Run(AllPlatformsScan(
                AddChild<MeshCollider>("A"), AddChild<BoxCollider>("B"), AddChild<Camera>("C"),
                AddChild<CapsuleCollider>("D"), AddChild<Light>("E"), AddChild<Rigidbody>("F")));

            var overflow = results.Last();
            StringAssert.StartsWith("ほか", overflow.Message);
            Assert.IsNull(overflow.TargetObject);
        }

        [Test]
        public void 深刻度はすべてWarningになる()
        {
            // アップロード自体は通るので Error ではない（Issue #36 の指定）
            var results = Run(AllPlatformsScan(AddChild<MeshCollider>("A"), AddChild<Light>("B")));

            Assert.IsNotEmpty(results);
            Assert.IsTrue(results.All(r => r.Severity == Severity.Warning));
        }

        [Test]
        public void 自動修正は提供しない()
        {
            // PC 版で意図して使っている可能性があり、勝手に消すと壊れる（Issue #36）
            var results = Run(AllPlatformsScan(AddChild<MeshCollider>("A")));

            Assert.IsTrue(results.All(r => !r.HasFix));
        }

        [Test]
        public void 実スキャナ経由でもMeshColliderを検出する()
        {
            // BuildResults 単体ではなく、QuestCompatibilityScanner の AllPlatforms 項目が
            // 実際にこのチェックへ流れてくることを固定する。
            // Scan(GameObject) はアクティブビルドターゲットに依存するため Standalone 相当を明示する
            AddChild<MeshCollider>("Mesh");

            var results = Run(QuestCompatibilityScanner.Scan(_root, isMobileBuildTarget: false));

            Assert.AreEqual(1, results.Count(r => !r.IsDetail));
            StringAssert.Contains("MeshCollider ×1", results.Single(r => r.IsDetail).Message);
        }

        [Test]
        public void 通常のアバター構成では実スキャナ経由でも何も出ない()
        {
            // 通常の改変アバターの構成要素はほぼすべて SDK のホワイトリスト側にある
            // （W0 実測: 実アバター3体で 0 件）。「普段は静か、踏んだときだけ出る」を壊さない。
            // BoxCollider は PC では動くので Quest スコープ側の担当であり、ここには出てこない
            AddChild<SkinnedMeshRenderer>("Body");
            AddChild<BoxCollider>("Collider");

            Assert.IsEmpty(Run(QuestCompatibilityScanner.Scan(_root, isMobileBuildTarget: false)));
        }
    }
}
