using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// 利用統計（<see cref="UsageStats"/>）のローカル記録を司る静的レコーダー（DEC-055 / 選択C: ローカル完結＋手動エクスポート）。
    ///
    /// <para>
    /// 【プライバシー方針】
    /// 保存先は <c>Library/com.tsukumodo.avatar-omamori/usage-stats.json</c>（プロジェクト単位・Git 追跡されない）。
    /// データの自動送信は一切行わない。ユーザーが「フィードバックとしてコピー」を押したときだけ
    /// クリップボードにコピーされ、本人の意思で DM 送付する導線のみ用意する。
    /// </para>
    /// <para>
    /// 【収集する / しない の境界をコードで保証】
    /// ・キー（チェック/修正の種別）は <see cref="SanitizeKey"/> を必ず通し、ASCII 識別子のみ許可する。
    ///   これにより、万一アバター名・パス・日本語などが渡ってもキーとして保存されない。
    /// ・日付は本クラス内で <see cref="Today"/>（年月日のみ）に整形する。呼び出し側から日時文字列を受け取らない＝時分秒が混入しない。
    /// ・ツールバージョンは package.json（自パッケージ）からのみ取得する。シーン・アセットを reflection で覗かない。
    /// ・チェック種別名は「自分のチェッククラスの型名」（<c>Type.Name</c>）だけを使う。ユーザーのアセットへ reflection しない。
    /// </para>
    /// </summary>
    public static class UsageStatsRecorder
    {
        private const string PackageFolderName = "com.tsukumodo.avatar-omamori";
        private const string StatsFileName = "usage-stats.json";
        private const int MaxKeyLength = 64;

        // 初回告知（1行 InfoBox）を表示済みかどうか。プロジェクト横断で1度きりにしたいので EditorPrefs（マシン単位）に置く。
        private const string NoticeAckPrefKey = "AvatarOmamori.UsageStats.NoticeAcknowledged";

        // プロセス内キャッシュ。Editor セッション中はプロジェクトが変わらないので一度ロードすれば十分。
        private static UsageStats s_cache;
        private static string s_toolVersionCache;

        // ───────────────────────────── 記録 API ─────────────────────────────

        /// <summary>
        /// チェック一括実行（<see cref="CheckRunner.RunAll"/>）1回分を記録する。
        /// <paramref name="detectionsByCheckType"/> はチェッククラス名 → そのチェックが今回検出した件数。
        /// opt-out 中は何もしない。
        /// </summary>
        public static void RecordCheckRun(IReadOnlyDictionary<string, int> detectionsByCheckType)
        {
            var stats = Load();
            if (stats.OptOut) return;

            stats.CheckRunCount++;
            if (detectionsByCheckType != null)
            {
                foreach (var kv in detectionsByCheckType)
                {
                    if (kv.Value <= 0) continue;
                    var key = SanitizeKey(kv.Key);
                    if (key == null) continue; // 識別子として不正なキーは捨てる（個人情報混入の最終防波堤）
                    stats.DetectionCounts.TryGetValue(key, out int cur);
                    stats.DetectionCounts[key] = cur + kv.Value;
                }
            }

            Touch(stats);
            Save(stats);
        }

        /// <summary>
        /// 自動修正1回分を記録する。<paramref name="checkTypeName"/> は修正を提供したチェッククラス名。
        /// opt-out 中は何もしない。
        /// </summary>
        public static void RecordFix(string checkTypeName)
        {
            var stats = Load();
            if (stats.OptOut) return;

            var key = SanitizeKey(checkTypeName);
            if (key == null) return;

            stats.FixRunCounts.TryGetValue(key, out int cur);
            stats.FixRunCounts[key] = cur + 1;

            Touch(stats);
            Save(stats);
        }

        // ───────────────────────────── 操作 API（ウィンドウから） ─────────────────────────────

        /// <summary>現在の統計のスナップショット（破壊防止のためのコピー）を返す。</summary>
        public static UsageStats GetSnapshot() => Load().Clone();

        /// <summary>現在 opt-out（収集無効）かどうか。</summary>
        public static bool IsOptedOut => Load().OptOut;

        /// <summary>収集の有効 / 無効を切り替えて永続化する。</summary>
        public static void SetOptOut(bool optOut)
        {
            var stats = Load();
            stats.OptOut = optOut;
            // バージョンだけは最新に保っておく（無効化時点のバージョンが残るより現状を反映する方が自然）
            stats.ToolVersion = ResolveToolVersion();
            Save(stats);
        }

        /// <summary>
        /// 蓄積したカウント・日付を消去する（opt-out 設定は保持）。
        /// 「統計をクリア」ボタン用。次回記録時に first_run から振り直しになる。
        /// </summary>
        public static void ClearStats()
        {
            var stats = Load();
            bool keepOptOut = stats.OptOut;

            var fresh = new UsageStats
            {
                SchemaVersion = UsageStats.CurrentSchemaVersion,
                ToolVersion = ResolveToolVersion(),
                OptOut = keepOptOut,
            };
            s_cache = fresh;
            Save(fresh);
        }

        /// <summary>
        /// 「フィードバックとしてコピー」用。last_exported_at を今日に更新して保存し、
        /// 保存後の JSON 文字列（クリップボードに入れる中身）を返す。
        /// </summary>
        public static string MarkExportedAndGetJson()
        {
            var stats = Load();
            stats.LastExportedAt = Today();
            Save(stats);
            return ToJson(stats);
        }

        /// <summary>現在の統計を JSON 文字列で返す（保存はしない）。</summary>
        public static string GetJson() => ToJson(Load());

        // ───────────────────────────── 初回告知（T-8） ─────────────────────────────

        /// <summary>初回の1行 InfoBox をまだ出していないなら true。</summary>
        public static bool ShouldShowFirstRunNotice => !EditorPrefs.GetBool(NoticeAckPrefKey, false);

        /// <summary>初回告知を「見た」ことにして次回以降は出さない。</summary>
        public static void AcknowledgeNotice() => EditorPrefs.SetBool(NoticeAckPrefKey, true);

        // ───────────────────────────── 内部実装 ─────────────────────────────

        /// <summary>キャッシュ済みモデルを返す。未ロードならディスクから読む（無ければ既定値）。</summary>
        private static UsageStats Load()
        {
            if (s_cache != null) return s_cache;
            s_cache = LoadFromDisk();
            return s_cache;
        }

        private static UsageStats LoadFromDisk()
        {
            try
            {
                var path = StatsPath();
                if (!File.Exists(path))
                {
                    return new UsageStats { ToolVersion = ResolveToolVersion() };
                }

                var json = File.ReadAllText(path, Encoding.UTF8);
                var dto = JsonUtility.FromJson<Dto>(json);
                if (dto == null)
                {
                    return new UsageStats { ToolVersion = ResolveToolVersion() };
                }
                return FromDto(dto);
            }
            catch (Exception e)
            {
                // 壊れた / 読めないファイルは初期化して続行する（収集はおまけ機能なので失敗で止めない）。
                Debug.LogWarning($"[AvatarOmamori] 利用統計ファイルの読み込みに失敗しました。初期化します。{e.Message}");
                return new UsageStats { ToolVersion = ResolveToolVersion() };
            }
        }

        private static void Save(UsageStats stats)
        {
            s_cache = stats;
            try
            {
                var dir = StatsDir();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                // GitHub Actions の JSON パーサーは BOM を受け付けないため、BOM なし UTF-8 で書く（実装ノート参照）。
                File.WriteAllText(StatsPath(), ToJson(stats), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] 利用統計ファイルの保存に失敗しました。{e.Message}");
            }
        }

        /// <summary>first_run / last_run / tool_version を「今・現バージョン」に更新する。</summary>
        private static void Touch(UsageStats stats)
        {
            var today = Today();
            if (string.IsNullOrEmpty(stats.FirstRun)) stats.FirstRun = today;
            stats.LastRun = today;
            stats.ToolVersion = ResolveToolVersion();
            stats.SchemaVersion = UsageStats.CurrentSchemaVersion;
        }

        /// <summary>年月日のみの日付文字列。ロケール非依存にして元号表記等の混入を防ぐ。</summary>
        private static string Today() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>
        /// キーを ASCII 識別子（英数字とアンダースコア）に限定する。条件を満たさなければ null。
        /// 個人情報（アバター名・パス・日本語など）がキーとして保存されるのを構造的に防ぐ最終防波堤。
        /// </summary>
        private static string SanitizeKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            if (raw.Length > MaxKeyLength) return null;
            foreach (char c in raw)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return null;
            }
            return raw;
        }

        private static string ResolveToolVersion()
        {
            if (!string.IsNullOrEmpty(s_toolVersionCache)) return s_toolVersionCache;
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UsageStatsRecorder).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.version))
                {
                    s_toolVersionCache = pkg.version;
                    return s_toolVersionCache;
                }
            }
            catch
            {
                // PackageManager から取れない環境（開発中の埋め込み等）はフォールバック
            }
            s_toolVersionCache = "0.0.0-dev";
            return s_toolVersionCache;
        }

        private static string StatsDir()
        {
            // Application.dataPath は "<project>/Assets"。その親が <project>、さらに Library を足す。
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, "Library", PackageFolderName);
        }

        private static string StatsPath() => Path.Combine(StatsDir(), StatsFileName);

        // ───────────────────────────── JSON 直列化（JsonUtility 用 DTO） ─────────────────────────────
        //
        // JsonUtility は Dictionary を直列化できないため、key/count のリストに展開して読み書きする。
        // フィールド名がそのまま JSON のキーになるので、DEC-055 のスキーマ名に合わせている。

        private static string ToJson(UsageStats stats)
        {
            return JsonUtility.ToJson(ToDto(stats), prettyPrint: true);
        }

        private static Dto ToDto(UsageStats s)
        {
            return new Dto
            {
                schema_version = s.SchemaVersion,
                tool_version = s.ToolVersion ?? "",
                first_run = s.FirstRun ?? "",
                last_run = s.LastRun ?? "",
                opt_out = s.OptOut,
                check_run_count = s.CheckRunCount,
                detection_counts = ToEntries(s.DetectionCounts),
                fix_run_counts = ToEntries(s.FixRunCounts),
                last_exported_at = s.LastExportedAt ?? "",
            };
        }

        private static UsageStats FromDto(Dto d)
        {
            return new UsageStats
            {
                SchemaVersion = d.schema_version == 0 ? UsageStats.CurrentSchemaVersion : d.schema_version,
                ToolVersion = d.tool_version ?? "",
                FirstRun = d.first_run ?? "",
                LastRun = d.last_run ?? "",
                OptOut = d.opt_out,
                CheckRunCount = d.check_run_count,
                DetectionCounts = FromEntries(d.detection_counts),
                FixRunCounts = FromEntries(d.fix_run_counts),
                LastExportedAt = d.last_exported_at ?? "",
            };
        }

        // 件数が多い順 → キー昇順で安定ソート。ファイルを決定的にし、「多い順」で読みやすくする。
        private static List<CountEntry> ToEntries(Dictionary<string, int> dict)
        {
            var list = new List<CountEntry>();
            if (dict == null) return list;
            foreach (var kv in dict.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
            {
                list.Add(new CountEntry { key = kv.Key, count = kv.Value });
            }
            return list;
        }

        private static Dictionary<string, int> FromEntries(List<CountEntry> entries)
        {
            var dict = new Dictionary<string, int>();
            if (entries == null) return dict;
            foreach (var e in entries)
            {
                if (e == null) continue;
                var key = SanitizeKey(e.key);
                if (key == null) continue; // 読み込み時にも不正キーを弾く
                dict[key] = e.count;
            }
            return dict;
        }

        [Serializable]
        private sealed class Dto
        {
            public int schema_version;
            public string tool_version;
            public string first_run;
            public string last_run;
            public bool opt_out;
            public int check_run_count;
            public List<CountEntry> detection_counts;
            public List<CountEntry> fix_run_counts;
            public string last_exported_at;
        }

        [Serializable]
        private sealed class CountEntry
        {
            public string key;
            public int count;
        }
    }
}
