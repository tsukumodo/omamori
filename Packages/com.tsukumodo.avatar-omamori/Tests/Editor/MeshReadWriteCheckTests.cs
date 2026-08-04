using System.Collections.Generic;
using System.Linq;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;
using NUnit.Framework;
using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    public class MeshReadWriteCheckTests
    {
        private GameObject _root;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly MeshReadWriteCheck _check = new MeshReadWriteCheck();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Avatar");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (var mesh in _meshes)
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
            }
            _meshes.Clear();
        }

        /// <summary>
        /// テスト用のメッシュを作る。<paramref name="readable"/> が false のときは
        /// <c>UploadMeshData(true)</c> で CPU 側データを破棄し、isReadable == false の状態を作る。
        /// ⚠ ビルトインの Cube などを使い回すと、そのメッシュがセッション中ずっと壊れるため必ず新規に作る。
        /// </summary>
        private Mesh CreateMesh(bool readable)
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateNormals();
            _meshes.Add(mesh);

            if (!readable) mesh.UploadMeshData(true);
            return mesh;
        }

        private void AddMeshRenderer(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
        }

        [Test]
        public void 読み取り可能なメッシュは検出しない()
        {
            AddMeshRenderer("Body", CreateMesh(readable: true));

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void ReadWriteが無効なメッシュをErrorで検出する()
        {
            AddMeshRenderer("Body", CreateMesh(readable: false));

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Error, results[0].Severity);
            Assert.That(results[0].Message, Does.Contain("Read/Write"));
        }

        [Test]
        public void 同じメッシュを共有する複数のRendererでも1件にまとめる()
        {
            // 衣装などで同じメッシュが使い回されているとき、同じ問題を何度も見せない
            var shared = CreateMesh(readable: false);
            AddMeshRenderer("Body", shared);
            AddMeshRenderer("BodyCopy", shared);

            Assert.AreEqual(1, _check.Execute(_root).ToList().Count);
        }

        [Test]
        public void 別々のメッシュはそれぞれ検出する()
        {
            AddMeshRenderer("Body", CreateMesh(readable: false));
            AddMeshRenderer("Hair", CreateMesh(readable: false));

            Assert.AreEqual(2, _check.Execute(_root).ToList().Count);
        }

        [Test]
        public void SkinnedMeshRendererのメッシュも見る()
        {
            var go = new GameObject("SkinnedBody");
            go.transform.SetParent(_root.transform);
            go.AddComponent<SkinnedMeshRenderer>().sharedMesh = CreateMesh(readable: false);

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(go, results[0].TargetObject);
        }

        [Test]
        public void 非アクティブなオブジェクトのメッシュも検出する()
        {
            AddMeshRenderer("HiddenBody", CreateMesh(readable: false));
            _root.transform.GetChild(0).gameObject.SetActive(false);

            Assert.AreEqual(1, _check.Execute(_root).ToList().Count);
        }

        [Test]
        public void メッシュが設定されていないRendererは無視する()
        {
            var go = new GameObject("Empty");
            go.transform.SetParent(_root.transform);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }
    }
}
