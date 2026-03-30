using System.Collections.Generic;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// アバターのバリデーションチェックを定義するインターフェース。
    /// このインターフェースを実装したクラスは <see cref="CheckRunner"/> により
    /// リフレクションで自動検出・実行される。
    /// </summary>
    public interface IAvatarCheck
    {
        /// <summary>
        /// UIに表示されるチェックの名前。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// チェックが実行可能かどうかを返す。
        /// 依存パッケージが未インストールの場合など、実行不可の場合は false を返す。
        /// </summary>
        /// <returns>チェック実行可能な場合は true。</returns>
        bool IsAvailable();

        /// <summary>
        /// アバタールートに対してバリデーションチェックを実行し、検出結果を返す。
        /// </summary>
        /// <param name="avatarRoot">チェック対象のアバタールート GameObject。</param>
        /// <returns>検出された問題の一覧。問題がなければ空のシーケンスを返す。</returns>
        IEnumerable<CheckResult> Execute(GameObject avatarRoot);
    }
}
