using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class DescriptorDuplicateCheckTests
    {
        private GameObject _root;
        private readonly DescriptorDuplicateCheck _check = new DescriptorDuplicateCheck();

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Descriptorが1個なら検出しない()
        {
            _root = new GameObject("Avatar");
            _root.AddComponent<VRCAvatarDescriptor>();

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void Descriptorが2個ならサマリーと重複箇所の2件を検出する()
        {
            _root = new GameObject("Avatar");
            _root.AddComponent<VRCAvatarDescriptor>();
            var child = new GameObject("Child");
            child.transform.SetParent(_root.transform);
            child.AddComponent<VRCAvatarDescriptor>();

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(2, results.Count);

            var summary = results[0];
            Assert.AreEqual(Severity.Error, summary.Severity);
            Assert.That(summary.Message, Does.Contain("2 個"));
            Assert.IsTrue(summary.HasFix, "サマリー結果に修正アクションが付いていない");
            Assert.IsTrue(summary.SkipConfirm, "選択ウィンドウを出す修正は SkipConfirm であるべき");

            var duplicate = results[1];
            Assert.AreEqual(Severity.Error, duplicate.Severity);
            Assert.That(duplicate.Message, Does.Contain("Avatar/Child"));
            Assert.IsFalse(duplicate.HasFix, "重複箇所の結果に修正アクションは付かない（サマリーに集約）");
        }
    }
}
