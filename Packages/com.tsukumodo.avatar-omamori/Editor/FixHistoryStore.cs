using System.Collections.Generic;
using System.Linq;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// セッション内の自動修正履歴を保持するプロセスローカルのストア。
    /// FixAction クロージャからインスタンス参照なしで Record できるよう static にしている。
    /// アバター切替ではクリアしない（DEC-055 設計選択C）。
    /// Editor プロセス終了で消える（永続化しない）。
    /// </summary>
    internal static class FixHistoryStore
    {
        private const int MaxEntries = 100;

        // 古い順に push、上限超過で先頭から捨てる。表示は新しい順に返すので Entries で反転する
        private static readonly List<FixHistoryEntry> _entries = new List<FixHistoryEntry>();

        /// <summary>現在保持している履歴件数。</summary>
        public static int Count => _entries.Count;

        /// <summary>新しい順の履歴。</summary>
        public static IEnumerable<FixHistoryEntry> Entries => _entries.AsEnumerable().Reverse();

        /// <summary>履歴に1件追加する。上限超過時は最古を捨てる。</summary>
        public static void Record(FixHistoryEntry entry)
        {
            if (entry == null) return;
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
        }

        /// <summary>履歴を全消去する（UI からのクリア操作用）。</summary>
        public static void Clear()
        {
            _entries.Clear();
        }
    }
}
