using AvatarOmamori.Editor.Performance;
using NUnit.Framework;

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
    }
}
