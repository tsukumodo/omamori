using System.Collections.Generic;
using AvatarOmamori.Editor.Performance;
using NUnit.Framework;
using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// まとまり判定（v0.11.0 W0 設計 §11.3 のルール1〜6・DEC-109）のテスト。
    ///
    /// <para>
    /// プレハブ由来の判定は <see cref="IPrefabStructure"/> に切り出してあるので、
    /// プレハブ資産を作らずにルールそのものを検証できる。
    /// 実物のアバター（TestAvatar）での分かれ方は実機で照合する。
    /// </para>
    /// </summary>
    public class AvatarPartGroupingTests
    {
        private GameObject _root;
        private FakePrefabStructure _prefab;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
            _prefab = new FakePrefabStructure();
            _prefab.PrefabInstances.Add(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void ルール1_後から足したプレハブの一番外側が1まとまりになる()
        {
            var clothes = AddChild(_root, "Clothes");
            _prefab.Added.Add(clothes);
            _prefab.PrefabInstances.Add(clothes);
            _prefab.PrefabRoots.Add(clothes);
            var jacket = AddRenderer(AddChild(clothes, "Jacket"));
            var shoes = AddRenderer(AddChild(clothes, "Shoes"));

            var groups = Build(jacket, shoes);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("Clothes", groups[0].Name);
            Assert.AreEqual(2, groups[0].Renderers.Count);
        }

        [Test]
        public void ルール1_骨の下に足したプレハブも置き場所を問わずまとまりになる()
        {
            var armature = AddChild(_root, "Armature");
            var head = AddChild(armature, "Head");
            var glasses = AddChild(head, "Glasses");
            _prefab.Added.Add(glasses);
            _prefab.PrefabInstances.Add(glasses);
            _prefab.PrefabRoots.Add(glasses);
            var lens = AddRenderer(AddChild(glasses, "Lens"));

            var groups = Build(lens);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("Glasses", groups[0].Name);
        }

        [Test]
        public void ルール2_後から足した空の入れ物はまとまりにせず中身を1件ずつ出す()
        {
            // Sakuhi の「アクセサリー」のように、空のオブジェクトに装飾品を入れているケース。
            // 入れ物ごと1行にすると、どれを替えるかが判断できない
            var box = AddChild(_root, "アクセサリー");
            _prefab.Added.Add(box);
            var beret = AddRenderer(AddChild(box, "ベレー帽"));
            var horn = AddRenderer(AddChild(box, "角"));

            var groups = Build(beret, horn);

            CollectionAssert.AreEquivalent(
                new[] { "ベレー帽", "角" }, Names(groups));
            Assert.IsFalse(Names(groups).Contains("アクセサリー"));
        }

        [Test]
        public void ルール3_後から足した非プレハブでメッシュを持つものは1まとまりになる()
        {
            var cube = AddRenderer(AddChild(_root, "Cube"));
            _prefab.Added.Add(cube.gameObject);

            var groups = Build(cube);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("Cube", groups[0].Name);
        }

        [Test]
        public void ルール4_メッシュを1つも持たないまとまりは出さない()
        {
            // GogoLoco・FaceEmo のようなギミック。プレハブだが Renderer を持たない
            var gimmick = AddChild(_root, "GogoLoco");
            _prefab.Added.Add(gimmick);
            _prefab.PrefabInstances.Add(gimmick);
            _prefab.PrefabRoots.Add(gimmick);
            AddChild(gimmick, "Settings");

            var body = AddRenderer(AddChild(_root, "Body"));

            var groups = Build(body);

            Assert.IsFalse(Names(groups).Contains("GogoLoco"));
            Assert.AreEqual(1, groups.Count);
            Assert.IsTrue(groups[0].IsAvatarBody);
        }

        [Test]
        public void ルール5_元からあるものはアバター本体にまとめる()
        {
            var body = AddRenderer(AddChild(_root, "Body"));
            var hair = AddRenderer(AddChild(_root, "Hair"));
            var clothes = AddChild(_root, "Clothes");
            _prefab.Added.Add(clothes);
            _prefab.PrefabInstances.Add(clothes);
            _prefab.PrefabRoots.Add(clothes);
            var jacket = AddRenderer(AddChild(clothes, "Jacket"));

            var groups = Build(body, hair, jacket);

            Assert.AreEqual(2, groups.Count);
            var avatarBody = groups[groups.Count - 1];
            Assert.IsTrue(avatarBody.IsAvatarBody, "アバター本体は最後に置く");
            Assert.AreEqual(AvatarPartGrouping.AvatarBodyName, avatarBody.Name);
            Assert.AreEqual(2, avatarBody.Renderers.Count);
            Assert.IsNull(avatarBody.Root, "本体は外す対象ではないので実体を持たせない");
        }

        [Test]
        public void ルール6_Unpack済みなら直下の子ごとに分けアバター本体の見出しを出さない()
        {
            _prefab.PrefabInstances.Remove(_root);
            var body = AddRenderer(AddChild(_root, "Body"));
            var clothes = AddChild(_root, "Clothes");
            var jacket = AddRenderer(AddChild(clothes, "Jacket"));

            var groups = Build(body, jacket);

            CollectionAssert.AreEquivalent(new[] { "Body", "Clothes" }, Names(groups));
            foreach (var g in groups) Assert.IsFalse(g.IsAvatarBody);
        }

        [Test]
        public void ルール6_Unpack済みでルート直下のRendererも落とさない()
        {
            _prefab.PrefabInstances.Remove(_root);
            var onRoot = AddRenderer(_root);
            var child = AddRenderer(AddChild(_root, "Child"));

            var groups = Build(onRoot, child);

            CollectionAssert.AreEquivalent(new[] { "Avatar", "Child" }, Names(groups));
        }

        [Test]
        public void Rendererが無ければ空になる()
        {
            Assert.IsEmpty(AvatarPartGrouping.Build(_root, new List<Renderer>(), _prefab));
        }

        // --- helpers ---

        private IReadOnlyList<AvatarPartGroup> Build(params Renderer[] renderers)
        {
            return AvatarPartGrouping.Build(_root, renderers, _prefab);
        }

        private static List<string> Names(IReadOnlyList<AvatarPartGroup> groups)
        {
            var names = new List<string>();
            foreach (var g in groups) names.Add(g.Name);
            return names;
        }

        private static GameObject AddChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        private static Renderer AddRenderer(GameObject go)
        {
            return go.AddComponent<MeshRenderer>();
        }

        private sealed class FakePrefabStructure : IPrefabStructure
        {
            public readonly HashSet<GameObject> PrefabInstances = new HashSet<GameObject>();
            public readonly HashSet<GameObject> Added = new HashSet<GameObject>();
            public readonly HashSet<GameObject> PrefabRoots = new HashSet<GameObject>();

            public bool IsPrefabInstance(GameObject go)
            {
                return go != null && PrefabInstances.Contains(go);
            }

            public bool IsAddedObject(GameObject go)
            {
                return go != null && Added.Contains(go);
            }

            public bool IsPrefabInstanceRoot(GameObject go)
            {
                return go != null && PrefabRoots.Contains(go);
            }
        }
    }
}
