using System.Collections.Generic;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// ローカルに蓄積する利用統計の in-memory モデル（schema v1）。
    ///
    /// <para>
    /// 【このクラスが保持してよい情報の境界（DEC-055・コードで保証）】
    /// 保持する: チェック/修正の種別名（= チェッククラス名。ASCII 識別子のみ。<see cref="UsageStatsRecorder"/> でサニタイズ）、
    /// 各種カウント（整数）、日付（年月日のみ）、ツールバージョン、opt-out フラグ。
    /// 保持しない: アバター名・GUID・シーン名・PC名・ユーザー名・ファイルパス・時分秒。
    /// </para>
    /// <para>
    /// これらの「保持しない」項目は、そもそもこのクラスに格納できる口（フィールド）を作らないことで構造的に防ぐ。
    /// ディスク I/O・カウント・キーのサニタイズは <see cref="UsageStatsRecorder"/> が担う。
    /// </para>
    /// </summary>
    internal sealed class UsageStats
    {
        /// <summary>現在のスキーマバージョン。将来フォーマットを変えるときに移行判断へ使う。</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>このデータのスキーマバージョン。</summary>
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>記録時のツールバージョン（package.json 由来）。</summary>
        public string ToolVersion = "";

        /// <summary>初めて記録した日（"yyyy-MM-dd"・日付のみ）。</summary>
        public string FirstRun = "";

        /// <summary>最後に記録した日（"yyyy-MM-dd"・日付のみ）。</summary>
        public string LastRun = "";

        /// <summary>true なら以降の収集を停止する（ユーザーが「収集を無効化」したことの永続フラグ）。</summary>
        public bool OptOut = false;

        /// <summary>チェック一括実行（RunAll）の累計回数。</summary>
        public int CheckRunCount = 0;

        /// <summary>チェック種別ごとに検出した問題の累計件数。キーはチェッククラス名。</summary>
        public Dictionary<string, int> DetectionCounts = new Dictionary<string, int>();

        /// <summary>修正種別ごとの自動修正の累計実行回数。キーはチェッククラス名。</summary>
        public Dictionary<string, int> FixRunCounts = new Dictionary<string, int>();

        /// <summary>最後に「フィードバックとしてコピー」した日（"yyyy-MM-dd"）。未エクスポートは空文字。</summary>
        public string LastExportedAt = "";

        /// <summary>表示用に内部状態を壊さないためのディープコピーを返す。</summary>
        public UsageStats Clone()
        {
            return new UsageStats
            {
                SchemaVersion = SchemaVersion,
                ToolVersion = ToolVersion,
                FirstRun = FirstRun,
                LastRun = LastRun,
                OptOut = OptOut,
                CheckRunCount = CheckRunCount,
                DetectionCounts = new Dictionary<string, int>(DetectionCounts),
                FixRunCounts = new Dictionary<string, int>(FixRunCounts),
                LastExportedAt = LastExportedAt,
            };
        }
    }
}
