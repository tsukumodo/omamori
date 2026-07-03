using NUnit.Framework;
using UnityEngine;
using AvatarOmamori.Editor.Util;

namespace AvatarOmamori.Tests.Editor
{
    public class HierarchyPathUtilTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void GetHierarchyPath_3階層のパスをスラッシュ区切りで返す()
        {
            _root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(_root.transform);
            var target = new GameObject("Target");
            target.transform.SetParent(child.transform);

            Assert.AreEqual("Root/Child/Target", HierarchyPathUtil.GetHierarchyPath(target));
        }

        [Test]
        public void GetHierarchyPath_ルート単体は名前のみを返す()
        {
            _root = new GameObject("Root");

            Assert.AreEqual("Root", HierarchyPathUtil.GetHierarchyPath(_root));
        }
    }
}
