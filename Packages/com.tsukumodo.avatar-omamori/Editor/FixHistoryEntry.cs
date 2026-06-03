using System;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// 自動修正が1回実行されたことを記録する不変エントリ。
    /// FixAction の中で修正直前/直後の値を実測し、<see cref="FixHistoryStore"/> に積む。
    /// CheckResult の Before/After（構築時セットの「予告値」）とは別物で、
    /// fix 失敗や別ウィンドウでの手動変更で予告と実測が乖離した場合に、実測側を残すのが目的（DEC-055 選択C）。
    /// </summary>
    public sealed class FixHistoryEntry
    {
        /// <summary>修正が実行された時刻。</summary>
        public DateTime Timestamp { get; }

        /// <summary>修正を実行した Check の識別名（例: "AnimatorLayerWeightCheck"）。</summary>
        public string CheckName { get; }

        /// <summary>表示用の値ラベル（例: "FX Layer / Weight"）。</summary>
        public string ValueLabel { get; }

        /// <summary>修正直前に実測した値の文字列表現。</summary>
        public string BeforeValue { get; }

        /// <summary>修正直後に実測した値の文字列表現。</summary>
        public string AfterValue { get; }

        /// <summary>
        /// 修正対象オブジェクトの InstanceID。Ping のときに <see cref="UnityEditor.EditorUtility.InstanceIDToObject"/>
        /// で復元する。シーン切替や対象削除で 0 を返すようになることがある。
        /// </summary>
        public int TargetInstanceID { get; }

        /// <summary>
        /// 修正対象オブジェクトの名前。InstanceID から復元できなくなったときの表示フォールバック用。
        /// </summary>
        public string TargetObjectName { get; }

        public FixHistoryEntry(
            DateTime timestamp,
            string checkName,
            string valueLabel,
            string beforeValue,
            string afterValue,
            int targetInstanceID,
            string targetObjectName)
        {
            Timestamp = timestamp;
            CheckName = checkName;
            ValueLabel = valueLabel;
            BeforeValue = beforeValue;
            AfterValue = afterValue;
            TargetInstanceID = targetInstanceID;
            TargetObjectName = targetObjectName;
        }
    }
}
