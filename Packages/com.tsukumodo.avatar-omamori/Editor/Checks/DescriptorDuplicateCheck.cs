using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバタールート以下に VRC_AvatarDescriptor が複数存在する場合にエラーを報告する。
    /// 複数の Descriptor が存在するとアバターのビルド・アップロードに失敗する。
    /// </summary>
    public sealed class DescriptorDuplicateCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "VRC Avatar Descriptor 重複チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var descriptors = avatarRoot.GetComponentsInChildren<VRCAvatarDescriptor>(true);

            if (descriptors.Length <= 1)
                yield break;

            string summaryMessage =
                $"VRC Avatar Descriptor が {descriptors.Length} 個見つかりました。" +
                "複数あるとアバターのビルド・アップロードに失敗します。" +
                "「修正」ボタンで「どれを残すか」を選ぶウィンドウが開きます（選んだもの以外を自動削除、Undo 対応）。";

            yield return new CheckResult(
                Severity.Error,
                summaryMessage,
                avatarRoot,
                // avatarRoot / descriptors はメソッド引数とローカル変数で再代入されないため、ラムダで直接捕捉して問題ない
                fixAction: () => ShowResolveWindow(avatarRoot, descriptors),
                fixLabel: null,                    // 共通「修正」で統一
                fixConfirmMessage: null,           // SkipConfirm=true のとき未使用
                skipConfirm: true                  // 独自の選択ウィンドウを出すので事前確認・自動再チェックともスキップ
            );

            // 各重複箇所も検出行として報告（FixAction はサマリー側に集約済み）
            for (int i = 1; i < descriptors.Length; i++)
            {
                yield return new CheckResult(
                    Severity.Error,
                    $"重複した VRC Avatar Descriptor: {HierarchyPathUtil.GetHierarchyPath(descriptors[i].gameObject)}",
                    descriptors[i]
                );
            }
        }

        /// <summary>
        /// 「どの Descriptor を残すか」を選ぶウィンドウを中央表示する。
        /// 選択されたら、選ばれた Descriptor 以外を一括削除してからおまもりウィンドウを再チェックする。
        /// </summary>
        private static void ShowResolveWindow(
            GameObject avatarRoot,
            VRCAvatarDescriptor[] descriptors)
        {
            DescriptorChoiceWindow.Show(avatarRoot, descriptors, keep =>
            {
                RemoveAllExcept(descriptors, keep);
                RefreshOmamoriWindow();
            });
        }

        /// <summary>
        /// <paramref name="keep"/> 以外の Descriptor を一括削除する。
        /// 複数削除を1つの Undo 単位にまとめ、Ctrl+Z で一度に戻せるようにする。
        /// GameObject ごと削除すると他のコンポーネントや子を巻き込むので、コンポーネントだけを削除する。
        /// </summary>
        private static void RemoveAllExcept(
            VRCAvatarDescriptor[] all,
            VRCAvatarDescriptor keep)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Resolve Duplicate VRC Avatar Descriptors");
            int group = Undo.GetCurrentGroup();

            foreach (var d in all)
            {
                if (d != null && d != keep)
                {
                    Undo.DestroyObjectImmediate(d);
                }
            }

            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// 既存のおまもりウィンドウに再チェックを指示する。
        /// FixAction が非同期（選択ウィンドウ閉じた後に処理）なので、共通基盤の自動再チェックに乗れない。
        /// </summary>
        private static void RefreshOmamoriWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<AvatarOmamoriWindow>();
            foreach (var w in windows)
            {
                w.RefreshResults();
            }
        }
    }
}
