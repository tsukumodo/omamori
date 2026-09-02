using AvatarOmamori.Editor.Performance;
using NUnit.Framework;
using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// SDK 内部 API のリフレクション解決（<see cref="SdkPerformanceReflection.TryResolve"/>）のテスト。
    ///
    /// <para>
    /// <see cref="SdkPerformanceReflection.IsAvailable"/> は静的コンストラクタで1回だけ解決してキャッシュするため、
    /// テストから直接キャッシュを壊すことはできない。解決ロジック本体を型名引数付きの
    /// <see cref="SdkPerformanceReflection.TryResolve"/> として切り出してあるので、
    /// ここではそちらを意図的に壊れた型名で呼び、SDK の実体（VRC SDK のインストール状態）に依存せず検証する。
    /// </para>
    /// </summary>
    public class SdkPerformanceReflectionTests
    {
        [Test]
        public void 存在しない型名なら解決に失敗しMethodInfoは両方nullになる()
        {
            var resolved = SdkPerformanceReflection.TryResolve(
                "Nonexistent.Namespace.ClassName", out var polyCountMethod, out var analyzeMaterialsMethod);

            Assert.IsFalse(resolved);
            Assert.IsNull(polyCountMethod);
            Assert.IsNull(analyzeMaterialsMethod);
        }

        [Test]
        public void 空文字の型名でも例外を投げずに解決失敗を返す()
        {
            var resolved = SdkPerformanceReflection.TryResolve(
                string.Empty, out var polyCountMethod, out var analyzeMaterialsMethod);

            Assert.IsFalse(resolved);
            Assert.IsNull(polyCountMethod);
            Assert.IsNull(analyzeMaterialsMethod);
        }

        [Test]
        public void null型名でも例外を投げずに解決失敗を返す()
        {
            var resolved = SdkPerformanceReflection.TryResolve(
                null, out var polyCountMethod, out var analyzeMaterialsMethod);

            Assert.IsFalse(resolved);
            Assert.IsNull(polyCountMethod);
            Assert.IsNull(analyzeMaterialsMethod);
        }

        /// <summary>
        /// SDK の <c>CalculateRendererPolyCount</c> は <c>uint?</c> を返す。
        /// 呼び出し側が <c>int</c> で受けると型が一致せず、例外も出ないまま
        /// 「取得失敗＝0 ポリゴン」に落ちる（2026-09-02 の T-3 実測照合で実際に踏んだ）。
        /// リフレクション解決が通るかどうかだけを見ていても気付けないため、
        /// 実物の Mesh を持つ Renderer から 0 でない値が返ることをここで固定する。
        /// </summary>
        [Test]
        public void メッシュを持つRendererからポリゴン数が0でない値で取れる()
        {
            if (!SdkPerformanceReflection.IsAvailable)
            {
                Assert.Ignore("VRChat SDK の MeshPerformanceScanner を解決できないため検証をスキップする。");
            }

            var go = new GameObject("PolyCountProbe");
            Mesh mesh = null;
            try
            {
                mesh = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f)
                    },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();

                var taken = SdkPerformanceReflection.TryGetPolyCount(renderer, out var polyCount);

                Assert.IsTrue(taken, "ポリゴン数の取得に失敗した。SDK 側の戻り値の型が変わった可能性がある。");
                Assert.Greater(polyCount, 0, "三角形2枚のメッシュなのにポリゴン数が0になっている。");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Rendererがnullならポリゴン数の取得は失敗し0になる()
        {
            var taken = SdkPerformanceReflection.TryGetPolyCount(null, out var polyCount);

            Assert.IsFalse(taken);
            Assert.AreEqual(0, polyCount);
        }
    }
}
