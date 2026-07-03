using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    public class CheckRunnerRunAllTests
    {
        private string _statsDir;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            // RunAll は利用統計を記録するため、保存先を一時フォルダへ退避する
            _statsDir = UsageStatsTestUtil.BeginOverride();
            _root = new GameObject("Avatar");
        }

        [TearDown]
        public void TearDown()
        {
            UsageStatsTestUtil.EndOverride(_statsDir);
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void RunAll_例外を投げるチェックがあっても他のチェックは実行される()
        {
            var normal = new FakeCheck();
            var checks = new IAvatarCheck[] { new ThrowingCheck(), normal };

            LogAssert.Expect(LogType.Warning, new Regex("ThrowingCheck"));
            var results = CheckRunner.RunAll(_root, checks);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("fake", results[0].Message);
            Assert.IsTrue(normal.Executed);
        }

        [Test]
        public void RunAll_IsAvailableがfalseのチェックは実行されない()
        {
            var unavailable = new FakeCheck { Available = false };

            var results = CheckRunner.RunAll(_root, new IAvatarCheck[] { unavailable });

            Assert.IsEmpty(results);
            Assert.IsFalse(unavailable.Executed);
        }

        // ネストした private クラスなので CheckRunner.DiscoverChecks（メインアセンブリのみ走査）には
        // 拾われず、本物のチェック一覧を汚さない
        private sealed class FakeCheck : IAvatarCheck
        {
            public bool Available = true;
            public bool Executed;

            public string DisplayName => "Fake";

            public bool IsAvailable() => Available;

            public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
            {
                Executed = true;
                return new[] { new CheckResult(Severity.Info, "fake") };
            }
        }

        private sealed class ThrowingCheck : IAvatarCheck
        {
            public string DisplayName => "ThrowingCheck";

            public bool IsAvailable() => true;

            public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
            {
                throw new InvalidOperationException("boom");
            }
        }
    }
}
