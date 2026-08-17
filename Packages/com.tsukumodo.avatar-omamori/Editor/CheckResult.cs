using System;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// チェック結果の重要度。UI での色分けやソートに使用される。
    /// </summary>
    public enum Severity
    {
        /// <summary>ビルド・アップロードに失敗する、または重大な不具合を引き起こす問題。</summary>
        Error,
        /// <summary>意図しない挙動を引き起こす可能性がある問題。</summary>
        Warning,
        /// <summary>参考情報。修正は任意。</summary>
        Info
    }

    /// <summary>
    /// 個別のチェック結果を表すデータクラス。
    /// 重要度、メッセージ、問題の対象オブジェクトへの参照、および自動修正アクションを保持する。
    /// </summary>
    public sealed class CheckResult
    {
        /// <summary>この結果の重要度。</summary>
        public Severity Severity { get; }

        /// <summary>ユーザーに表示する問題の説明メッセージ。</summary>
        public string Message { get; }

        /// <summary>問題が検出された Unity オブジェクト。Hierarchy での選択・ハイライトに使用される。</summary>
        public UnityEngine.Object TargetObject { get; }

        /// <summary>自動修正を実行するアクション。null なら修正ボタンを表示しない。</summary>
        public Action FixAction { get; }

        /// <summary>修正ボタンの文言。null なら UI 側で "修正" を使う。</summary>
        public string FixLabel { get; }

        /// <summary>修正前の確認ダイアログ本文。null なら UI 側でデフォルト文言を使う。</summary>
        public string FixConfirmMessage { get; }

        /// <summary>
        /// true の場合、共通 UI の事前確認ダイアログと修正後の自動再チェックをスキップする。
        /// FixAction 内で独自のダイアログ・ドロップダウン等の非同期 UI を出す修正項目で、
        /// 二重ダイアログの回避と、UI 完了前の再チェック暴発の抑止を兼ねる。
        /// この場合、FixAction 側は完了時に <see cref="AvatarOmamoriWindow.RefreshResults"/> を呼ぶ責任を持つ。
        /// </summary>
        public bool SkipConfirm { get; }

        /// <summary>
        /// Before/After 表示の項目名（例: "FX Layer / Weight"）。null なら結果カードに値の前後を表示しない。
        /// 「直す力」を可視化する v0.6.0 の Before/After UI 用。
        /// </summary>
        public string ValueLabel { get; }

        /// <summary>修正前の値の文字列表現（例: "0"）。null 可。</summary>
        public string BeforeValue { get; }

        /// <summary>修正後の値の文字列表現（例: "1"）。null 可。</summary>
        public string AfterValue { get; }

        /// <summary>
        /// true の場合、直前のサマリー結果の「内訳」として扱う。
        /// UI では字下げして表示し、結果サマリー（"N Error"）・Foldout の件数・カード画像の件数には数えない。
        /// 1つの問題を複数行で説明するチェック（例: Descriptor 重複のサマリー＋各重複箇所）で、
        /// 件数が水増しされ、実際より多くの問題があるように見えるのを防ぐ。
        /// </summary>
        public bool IsDetail { get; }

        /// <summary>「どう直すか」の短いヒント。1行に収まる長さで書く（目安40〜60字）。</summary>
        public string Hint { get; }

        /// <summary>「調べる」ボタンで開く公式ドキュメント。null ならボタンを出さない。</summary>
        public string DocumentUrl { get; }

        /// <summary>自動修正が利用可能かどうか。</summary>
        public bool HasFix => FixAction != null;

        /// <summary>
        /// <see cref="CheckResult"/> の新しいインスタンスを作成する。
        /// </summary>
        /// <param name="severity">結果の重要度。</param>
        /// <param name="message">ユーザーに表示するメッセージ。</param>
        /// <param name="targetObject">問題の対象オブジェクト（任意）。</param>
        /// <param name="fixAction">自動修正アクション（任意）。null なら修正ボタン非表示。</param>
        /// <param name="fixLabel">修正ボタンの文言（任意）。null なら "修正"。</param>
        /// <param name="fixConfirmMessage">確認ダイアログ本文（任意）。null ならデフォルト文言。</param>
        /// <param name="skipConfirm">true にすると事前確認ダイアログを省略する（FixAction 側で独自にダイアログを出す場合用）。</param>
        /// <param name="valueLabel">Before/After 表示用の項目名（任意）。例: "FX Layer / Weight"。</param>
        /// <param name="beforeValue">修正前の値（任意）。例: "0"。</param>
        /// <param name="afterValue">修正後の値（任意）。例: "1"。</param>
        /// <param name="isDetail">true にすると直前のサマリー結果の内訳として扱い、件数に数えず字下げ表示する。</param>
        /// <param name="hint">「どう直すか」の短いヒント（任意）。null なら表示しない。</param>
        /// <param name="documentUrl">「調べる」ボタンで開く公式ドキュメント（任意）。null ならボタン非表示。</param>
        public CheckResult(
            Severity severity,
            string message,
            UnityEngine.Object targetObject = null,
            Action fixAction = null,
            string fixLabel = null,
            string fixConfirmMessage = null,
            bool skipConfirm = false,
            string valueLabel = null,
            string beforeValue = null,
            string afterValue = null,
            bool isDetail = false,
            string hint = null,
            string documentUrl = null)
        {
            Severity = severity;
            Message = message;
            TargetObject = targetObject;
            FixAction = fixAction;
            FixLabel = fixLabel;
            FixConfirmMessage = fixConfirmMessage;
            SkipConfirm = skipConfirm;
            ValueLabel = valueLabel;
            BeforeValue = beforeValue;
            AfterValue = afterValue;
            IsDetail = isDetail;
            Hint = hint;
            DocumentUrl = documentUrl;
        }
    }
}
