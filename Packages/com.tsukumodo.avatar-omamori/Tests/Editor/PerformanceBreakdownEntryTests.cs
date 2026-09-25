using AvatarOmamori.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 内訳ウィンドウへの入口（主画面のボタン）を出すかどうかの判定テスト（v0.11.0 T-5 / W0 設計 §5.2）。
    ///
    /// <para>
    /// ボタンを出しておいて開いたら空、は採らない。押した結果が空のウィンドウは壊れて見えるため、
    /// SDK 内部 API が解決できない環境ではボタンごと描かない。
    /// IMGUI の描画分岐そのものはテストから叩けないので、条件だけを検証する。
    /// ボタンが実際に1行だけで出ていること（主画面の縦幅の増分）の確認は実機（T-8）で行う。
    /// </para>
    /// </summary>
    public class PerformanceBreakdownEntryTests
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

        [Test]
        public void SDK内部APIが解決できなければボタンを出さない()
        {
            Assert.IsFalse(AvatarOmamoriWindow.ShouldShowBreakdownEntry(_root, sdkAvailable: false));
        }

        [Test]
        public void 対象アバターが未指定ならボタンを出さない()
        {
            Assert.IsFalse(AvatarOmamoriWindow.ShouldShowBreakdownEntry(null, sdkAvailable: true));
        }

        [Test]
        public void 対象がありSDKも使えるときだけボタンを出す()
        {
            Assert.IsTrue(AvatarOmamoriWindow.ShouldShowBreakdownEntry(_root, sdkAvailable: true));
        }

        [Test]
        public void 破棄済みのアバターではボタンを出さない()
        {
            var destroyed = new GameObject("Destroyed");
            Object.DestroyImmediate(destroyed);

            // 破棄済みオブジェクトは Unity の == オーバーロードで null 扱いになる。
            // 素の参照比較（ReferenceEquals）に変えると、シーンから消えたアバターにボタンが残る
            Assert.IsFalse(AvatarOmamoriWindow.ShouldShowBreakdownEntry(destroyed, sdkAvailable: true));
        }
    }
}
