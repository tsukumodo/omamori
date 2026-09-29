using System.Collections.Generic;
using AvatarOmamori.Editor;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// アバターの自動セット（v0.11.0 R-4 / DEC-109 / W0 設計 §11.4.2）の判定ロジックのテスト。
    /// <see cref="AvatarAutoSelector"/> の純粋関数だけを対象にする。EditorPrefs・シーン走査・
    /// ウィンドウの自動セット動作そのもの（OnEnable 等）は実機確認（R-5）で見る。
    /// </summary>
    public class AvatarAutoSelectorTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _created.Clear();
        }

        private GameObject NewAvatar(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<VRCAvatarDescriptor>();
            _created.Add(go);
            return go;
        }

        private GameObject NewPlain(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            _created.Add(go);
            return go;
        }

        // ───────────────────────────── PickOutermost ─────────────────────────────

        [Test]
        public void PickOutermost_1体だけなら選ばれる()
        {
            var avatar = NewAvatar("Avatar");

            var result = AvatarAutoSelector.PickOutermost(new[] { avatar }, selected: null);

            Assert.AreSame(avatar, result);
        }

        [Test]
        public void PickOutermost_入れ子のDescriptorは外側1体と判定される()
        {
            // TestAvatar のように、外側に1つ・内側に2つ Descriptor があるケース（§11.4.2 コメント準拠）。
            var outer = NewAvatar("Outer");
            var innerA = NewAvatar("InnerA", outer.transform);
            var innerB = NewAvatar("InnerB", innerA.transform);

            var result = AvatarAutoSelector.PickOutermost(
                new[] { outer, innerA, innerB }, selected: null);

            Assert.AreSame(outer, result);
        }

        [Test]
        public void PickOutermost_2体で選択なしならnull()
        {
            var avatarA = NewAvatar("AvatarA");
            var avatarB = NewAvatar("AvatarB");

            var result = AvatarAutoSelector.PickOutermost(
                new[] { avatarA, avatarB }, selected: null);

            Assert.IsNull(result);
        }

        [Test]
        public void PickOutermost_2体で片方の子を選択していればそのアバターが選ばれる()
        {
            var avatarA = NewAvatar("AvatarA");
            var avatarB = NewAvatar("AvatarB");
            var childOfB = NewPlain("ChildOfB", avatarB.transform);

            var result = AvatarAutoSelector.PickOutermost(
                new[] { avatarA, avatarB }, selected: childOfB);

            Assert.AreSame(avatarB, result);
        }

        [Test]
        public void PickOutermost_2体で選択がどちらにも属さなければnull()
        {
            var avatarA = NewAvatar("AvatarA");
            var avatarB = NewAvatar("AvatarB");
            var unrelated = NewPlain("Unrelated");

            var result = AvatarAutoSelector.PickOutermost(
                new[] { avatarA, avatarB }, selected: unrelated);

            Assert.IsNull(result);
        }

        [Test]
        public void PickOutermost_選択中のオブジェクト自身がDescriptor所有者ならそれが選ばれる()
        {
            var avatarA = NewAvatar("AvatarA");
            var avatarB = NewAvatar("AvatarB");

            var result = AvatarAutoSelector.PickOutermost(
                new[] { avatarA, avatarB }, selected: avatarB);

            Assert.AreSame(avatarB, result);
        }

        [Test]
        public void PickOutermost_null要素と破棄済み要素は無視される()
        {
            var avatar = NewAvatar("Avatar");
            var destroyed = NewAvatar("Destroyed");
            _created.Remove(destroyed);
            Object.DestroyImmediate(destroyed);

            var result = AvatarAutoSelector.PickOutermost(
                new[] { avatar, null, destroyed }, selected: null);

            Assert.AreSame(avatar, result);
        }

        // ───────────────────────────── FindOutermostOwners / IsStrictAncestor ─────────────────────────────

        [Test]
        public void FindOutermostOwners_入れ子構造で外側だけが残る()
        {
            var outer = NewAvatar("Outer");
            var inner = NewAvatar("Inner", outer.transform);

            var result = AvatarAutoSelector.FindOutermostOwners(new[] { outer, inner });

            CollectionAssert.AreEquivalent(new[] { outer }, result);
        }

        [Test]
        public void IsStrictAncestor_自分自身は祖先に含まない()
        {
            var avatar = NewAvatar("Avatar");

            Assert.IsFalse(AvatarAutoSelector.IsStrictAncestor(avatar.transform, avatar.transform));
        }

        [Test]
        public void IsStrictAncestor_親は祖先と判定される()
        {
            var parent = NewPlain("Parent");
            var child = NewPlain("Child", parent.transform);

            Assert.IsTrue(AvatarAutoSelector.IsStrictAncestor(parent.transform, child.transform));
            Assert.IsFalse(AvatarAutoSelector.IsStrictAncestor(child.transform, parent.transform));
        }

        // ───────────────────────────── ShouldRecordUsage ─────────────────────────────

        [Test]
        public void ShouldRecordUsage_Autoだけfalse()
        {
            Assert.IsFalse(AvatarAutoSelector.ShouldRecordUsage(AvatarAutoSelector.CheckTrigger.Auto));
            Assert.IsTrue(AvatarAutoSelector.ShouldRecordUsage(AvatarAutoSelector.CheckTrigger.Manual));
            Assert.IsTrue(AvatarAutoSelector.ShouldRecordUsage(AvatarAutoSelector.CheckTrigger.Button));
            Assert.IsTrue(AvatarAutoSelector.ShouldRecordUsage(AvatarAutoSelector.CheckTrigger.Refresh));
        }
    }

    /// <summary>
    /// 自動で入れたときの実行が利用統計に数えられないことを、主画面の実コード
    /// （<c>SetAvatarRootAndRunChecks</c> → <c>RunChecks</c>）を通して固定する（W0 設計 §11.4.2 の4）。
    /// </summary>
    public class AvatarAutoSelectorUsageStatsTests
    {
        private string _dir;
        private AvatarOmamoriWindow _window;
        private GameObject _avatar;

        [SetUp]
        public void SetUp()
        {
            _dir = UsageStatsTestUtil.BeginOverride();
            _window = ScriptableObject.CreateInstance<AvatarOmamoriWindow>();
            _avatar = new GameObject("AutoSelectStatsAvatar");
            _avatar.AddComponent<VRCAvatarDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
            if (_avatar != null) Object.DestroyImmediate(_avatar);
            UsageStatsTestUtil.EndOverride(_dir);
        }

        [Test]
        public void 自動で入れたときはcheck_run_countが増えない()
        {
            _window.SetAvatarRootAndRunChecks(_avatar, AvatarAutoSelector.CheckTrigger.Auto, persist: false);

            Assert.AreEqual(0, UsageStatsRecorder.GetSnapshot().CheckRunCount);
        }

        [Test]
        public void 手で指定したときはcheck_run_countが1増える()
        {
            _window.SetAvatarRootAndRunChecks(_avatar, AvatarAutoSelector.CheckTrigger.Manual, persist: false);

            Assert.AreEqual(1, UsageStatsRecorder.GetSnapshot().CheckRunCount);
        }
    }
}
