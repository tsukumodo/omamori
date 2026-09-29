using AvatarOmamori.Editor.Performance;
using NUnit.Framework;
using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// パフォーマンス内訳（差分方式）のテスト。
    ///
    /// <para>
    /// 差分の帰属計算そのもの（<see cref="PerformanceBreakdownBuilder.ReductionFromDiff"/>）は
    /// SDK の <c>AnalyzeMaterials</c> 呼び出しから切り離した純粋関数として書かれているため、
    /// SDK 未インストール環境でも検証できる。
    /// Renderer 収集（EditorOnly 除外）も SDK に依存しない
    /// <see cref="PerformanceReportBuilder.CollectRenderers"/> を直接検証する。
    /// SDK の実体（<see cref="SdkPerformanceReflection"/> 経由の実測）が要るテストはここには書かない。
    /// </para>
    /// </summary>
    public class PerformanceBreakdownBuilderTests
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
        public void 共有テクスチャしか持たないパーツの削減量は0になる()
        {
            // そのパーツを除いても総量が変わらない＝他のパーツとテクスチャを共有しているケース。
            // 単独帰属方式ならここで正の値が出てしまう（v0.11.0 W0 設計 §2.2 が却下した理由そのもの）
            var reduction = PerformanceBreakdownBuilder.ReductionFromDiff(
                totalValue: 141.8411f, valueWithoutPart: 141.8411f);

            Assert.AreEqual(0f, reduction);
        }

        [Test]
        public void 単独で使っているテクスチャの削減量は総量との差になる()
        {
            var reduction = PerformanceBreakdownBuilder.ReductionFromDiff(
                totalValue: 141.8411f, valueWithoutPart: 100f);

            Assert.AreEqual(41.8411f, reduction, 0.0001f);
        }

        [Test]
        public void 丸め誤差でわずかに負になっても0に丸める()
        {
            var reduction = PerformanceBreakdownBuilder.ReductionFromDiff(
                totalValue: 100f, valueWithoutPart: 100.0001f);

            Assert.AreEqual(0f, reduction);
        }

        [Test]
        public void EditorOnlyタグの親を辿ってRendererを除外する()
        {
            var editorOnlyGroup = new GameObject("EditorOnlyGroup");
            editorOnlyGroup.tag = "EditorOnly";
            editorOnlyGroup.transform.SetParent(_root.transform);

            var nested = new GameObject("Nested");
            nested.transform.SetParent(editorOnlyGroup.transform);
            var excludedRenderer = nested.AddComponent<MeshRenderer>();

            var visible = new GameObject("Visible");
            visible.transform.SetParent(_root.transform);
            var includedRenderer = visible.AddComponent<MeshRenderer>();

            var renderers = PerformanceReportBuilder.CollectRenderers(_root);

            CollectionAssert.Contains(renderers, includedRenderer);
            CollectionAssert.DoesNotContain(renderers, excludedRenderer);
        }

        [Test]
        public void ルート自体がEditorOnlyタグなら全Rendererが除外される()
        {
            _root.tag = "EditorOnly";
            var child = new GameObject("Child");
            child.transform.SetParent(_root.transform);
            child.AddComponent<MeshRenderer>();

            var renderers = PerformanceReportBuilder.CollectRenderers(_root);

            Assert.IsEmpty(renderers);
        }

        [Test]
        public void Rendererが1件も無ければ空リストになる()
        {
            var renderers = PerformanceReportBuilder.CollectRenderers(_root);

            Assert.IsEmpty(renderers);
        }
    }
}
