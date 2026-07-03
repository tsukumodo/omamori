using System;
using System.IO;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 利用統計の保存先を一時フォルダへ退避させるテスト用ヘルパー。
    /// 実プロジェクトの Library/ を汚さず、テスト間の状態も分離する。
    /// </summary>
    internal static class UsageStatsTestUtil
    {
        /// <summary>一時フォルダを作って保存先を差し替え、そのパスを返す。</summary>
        public static string BeginOverride()
        {
            var dir = Path.Combine(Path.GetTempPath(), "omamori-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            UsageStatsRecorder.ResetForTests();
            UsageStatsRecorder.StatsDirOverrideForTests = dir;
            return dir;
        }

        /// <summary>保存先の差し替えを解除し、一時フォルダを削除する。</summary>
        public static void EndOverride(string dir)
        {
            UsageStatsRecorder.StatsDirOverrideForTests = null;
            UsageStatsRecorder.ResetForTests();
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
