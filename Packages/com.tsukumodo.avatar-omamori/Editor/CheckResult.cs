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
        public CheckResult(
            Severity severity,
            string message,
            UnityEngine.Object targetObject = null,
            Action fixAction = null,
            string fixLabel = null,
            string fixConfirmMessage = null)
        {
            Severity = severity;
            Message = message;
            TargetObject = targetObject;
            FixAction = fixAction;
            FixLabel = fixLabel;
            FixConfirmMessage = fixConfirmMessage;
        }
    }
}
