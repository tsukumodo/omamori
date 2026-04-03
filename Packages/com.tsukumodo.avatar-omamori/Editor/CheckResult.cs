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
    /// 重要度、メッセージ、および問題の対象オブジェクトへの参照を保持する。
    /// </summary>
    public sealed class CheckResult
    {
        /// <summary>この結果の重要度。</summary>
        public Severity Severity { get; }

        /// <summary>ユーザーに表示する問題の説明メッセージ。</summary>
        public string Message { get; }

        /// <summary>問題が検出された Unity オブジェクト。Hierarchy での選択・ハイライトに使用される。</summary>
        public Object TargetObject { get; }

        /// <summary>
        /// <see cref="CheckResult"/> の新しいインスタンスを作成する。
        /// </summary>
        /// <param name="severity">結果の重要度。</param>
        /// <param name="message">ユーザーに表示するメッセージ。</param>
        /// <param name="targetObject">問題の対象オブジェクト（任意）。</param>
        public CheckResult(Severity severity, string message, Object targetObject = null)
        {
            Severity = severity;
            Message = message;
            TargetObject = targetObject;
        }
    }
}
