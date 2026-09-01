using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRC.SDKBase.Validation.Performance.Stats;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// VRChat SDK の <c>MeshPerformanceScanner</c> にある <c>private static</c> メソッド2本を
    /// リフレクション経由で呼び出すための集約クラス。
    ///
    /// <para>
    /// <c>MAReflectionHelper</c> と同じ方針で、他のクラスから直接このリフレクションを呼ばせない。
    /// 呼び出したいメソッド2本のどちらかが解決できない場合、<see cref="IsAvailable"/> は
    /// 必ず false になる（片方だけ取れた状態を許さない・v0.11.0 W0 設計 §5.2）。
    /// パーツ別の内訳はポリゴン数とテクスチャ使用量の両方が揃って初めて意味を持つため、
    /// 片方だけ出す内訳は「もう片方が無い理由」をユーザーに説明できず、かえって不親切になる。
    /// </para>
    /// </summary>
    internal static class SdkPerformanceReflection
    {
        private const string MeshPerformanceScannerTypeName =
            "VRC.SDKBase.Validation.Performance.Scanners.MeshPerformanceScanner";

        private static readonly MethodInfo s_calculateRendererPolyCountMethod;
        private static readonly MethodInfo s_analyzeMaterialsMethod;
        private static readonly bool s_isAvailable;

        static SdkPerformanceReflection()
        {
            s_isAvailable = TryResolve(
                MeshPerformanceScannerTypeName,
                out s_calculateRendererPolyCountMethod,
                out s_analyzeMaterialsMethod);

            if (!s_isAvailable)
            {
                Debug.LogWarning(
                    "[AvatarOmamori] SDK のパフォーマンス計測 API（MeshPerformanceScanner の"
                    + " CalculateRendererPolyCount / AnalyzeMaterials）を解決できませんでした。"
                    + "パーツ別の内訳は利用できません。");
            }
        }

        /// <summary>
        /// 2本の <see cref="MethodInfo"/> が両方解決できたかどうか。
        /// 静的初期化で1回だけ解決してキャッシュする。
        /// </summary>
        public static bool IsAvailable => s_isAvailable;

        /// <summary>
        /// <paramref name="typeName"/> から <c>MeshPerformanceScanner</c> 相当の型を引き、
        /// <c>CalculateRendererPolyCount(Renderer)</c> と
        /// <c>AnalyzeMaterials(List&lt;Renderer&gt;, AvatarPerformanceStats)</c> を解決する。
        ///
        /// <para>
        /// 解決ロジック本体をこの形（引数で型名を受け取る）に切り出しているのは、
        /// <see cref="IsAvailable"/> の静的キャッシュを直接壊せないテストから、
        /// わざと壊れた型名を渡して解決失敗のふるまいを確認できるようにするため。
        /// </para>
        /// </summary>
        internal static bool TryResolve(string typeName, out MethodInfo polyCountMethod, out MethodInfo analyzeMaterialsMethod)
        {
            polyCountMethod = null;
            analyzeMaterialsMethod = null;

            if (string.IsNullOrEmpty(typeName)) return false;

            var scannerType = PerformanceReportBuilder.FindType(typeName);
            if (scannerType == null) return false;

            polyCountMethod = scannerType.GetMethod(
                "CalculateRendererPolyCount",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Renderer) },
                null);

            analyzeMaterialsMethod = scannerType.GetMethod(
                "AnalyzeMaterials",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(List<Renderer>), typeof(AvatarPerformanceStats) },
                null);

            return polyCountMethod != null && analyzeMaterialsMethod != null;
        }

        /// <summary>
        /// <c>MeshPerformanceScanner.CalculateRendererPolyCount(Renderer)</c> を呼ぶ。
        /// </summary>
        public static bool TryGetPolyCount(Renderer renderer, out int polyCount)
        {
            polyCount = 0;
            if (!s_isAvailable || renderer == null) return false;

            try
            {
                var result = s_calculateRendererPolyCountMethod.Invoke(null, new object[] { renderer });
                if (result is int count)
                {
                    polyCount = count;
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] CalculateRendererPolyCount の呼び出しに失敗しました。{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// <c>MeshPerformanceScanner.AnalyzeMaterials(List&lt;Renderer&gt;, AvatarPerformanceStats)</c> を呼ぶ。
        /// 第1引数は <see cref="List{T}"/> の実体でないと <see cref="MethodInfo.Invoke"/> が失敗する
        /// （<c>IReadOnlyList</c> や配列では通らない・8/8 検証で確認済み）。
        /// </summary>
        public static bool TryAnalyzeMaterials(List<Renderer> renderers, AvatarPerformanceStats stats)
        {
            if (!s_isAvailable || renderers == null || stats == null) return false;

            try
            {
                s_analyzeMaterialsMethod.Invoke(null, new object[] { renderers, stats });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] AnalyzeMaterials の呼び出しに失敗しました。{e.Message}");
                return false;
            }
        }
    }
}
