using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace AvatarOmamori.Editor
{
    /// <summary>
    /// アバター改変おまもり主画面の「対象を覚える／空なら自動で選ぶ」ロジック（v0.11.0 W1 R-4 / DEC-109 / W0 設計 §11.4）。
    ///
    /// <para>
    /// どれを選ぶか・記録するかどうかの判定は、Unity のシーン・EditorPrefs に触れない
    /// internal static 純粋関数として切り出してある（<see cref="PickOutermost"/> / <see cref="FindOutermostOwners"/> /
    /// <see cref="IsStrictAncestor"/> / <see cref="ShouldRecordUsage"/>）。EditorPrefs 永続化・シーン走査など
    /// 副作用を伴う部分だけ、このクラスの他のメソッド（<see cref="SaveLastAvatarRoot"/> 等）に残す。
    /// </para>
    /// </summary>
    internal static class AvatarAutoSelector
    {
        // EditorPrefs キーの接頭辞。EditorPrefs はマシン単位でプロジェクトも横断するため、
        // シーンパスで区切って「別プロジェクト・別シーンの対象を誤って復元する」ことを防ぐ。
        private const string LastAvatarRootPrefKeyPrefix = "AvatarOmamori.LastAvatarRoot.";

        /// <summary>
        /// チェックを走らせた「きっかけ」。<see cref="ShouldRecordUsage"/> の判定にのみ使う。
        /// </summary>
        internal enum CheckTrigger
        {
            /// <summary>アバタールートの ObjectField を手で変更した。</summary>
            Manual,

            /// <summary>「チェック実行」ボタンを押した。</summary>
            Button,

            /// <summary>RefreshResults（FixAction 完了後の再チェック等）から呼ばれた。</summary>
            Refresh,

            /// <summary>
            /// 自動で対象を入れた（OnEnable / OnFocus / sceneOpened / OnSelectionChange）、
            /// またはドメインリロード後に _avatarRoot だけ復元されて結果を作り直した。
            /// </summary>
            Auto,
        }

        /// <summary>
        /// <paramref name="trigger"/> による実行を利用統計に数えるか。
        ///
        /// <para>
        /// v0.11.0 から、自動で入れたとき（<see cref="CheckTrigger.Auto"/>）の実行は数えない。
        /// 数えると再コンパイル・再生モード往復のたびに check_run_count と検出件数が増えてしまうため
        /// （W0 設計 §11.4.2 決定4）。副作用として、これまで「指定し直し」で数えていた分のうち自動選択・自動復元に
        /// よるものは対象外になるので、v0.11.0 から実行回数の数え方が変わる（記録に残す・DEC-109）。
        /// </para>
        /// </summary>
        internal static bool ShouldRecordUsage(CheckTrigger trigger) => trigger != CheckTrigger.Auto;

        /// <summary>
        /// <paramref name="candidateAncestor"/> が <paramref name="target"/> の祖先かどうか。
        /// <paramref name="target"/> 自身は祖先に含めない（厳密な祖先判定）。
        /// </summary>
        internal static bool IsStrictAncestor(Transform candidateAncestor, Transform target)
        {
            if (candidateAncestor == null || target == null) return false;

            var t = target.parent;
            while (t != null)
            {
                if (t == candidateAncestor) return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>
        /// <paramref name="descriptorOwners"/>（VRCAvatarDescriptor が付いた GameObject の集合）のうち、
        /// 祖先に別の所有者を持たない「いちばん外側」のものだけを返す（W0 設計 §11.3 / §11.4.2 と同じ考え方）。
        /// 入れ子の Descriptor（TestAvatar 等）は外側1つだけが残る。null・破棄済み・重複は無視する。純粋関数。
        /// </summary>
        internal static List<GameObject> FindOutermostOwners(IEnumerable<GameObject> descriptorOwners)
        {
            var owners = (descriptorOwners ?? Enumerable.Empty<GameObject>())
                .Where(o => o != null)
                .Distinct()
                .ToList();

            var result = new List<GameObject>();
            foreach (var owner in owners)
            {
                var hasAncestorOwner = owners.Any(other =>
                    other != owner && IsStrictAncestor(other.transform, owner.transform));
                if (!hasAncestorOwner)
                    result.Add(owner);
            }
            return result;
        }

        /// <summary>
        /// 空のときに自動で選ぶ対象を決める（W0 設計 §11.4.2 の2）。
        /// EditorPrefs からの復元判定はこの関数の外側（<see cref="SaveLastAvatarRoot"/> /
        /// <see cref="TryRestoreLastAvatarRoot"/>）で先に済ませる前提で、ここでは
        /// 「いちばん外側の Descriptor が1体だけなら確定」「複数なら選択中オブジェクトが属するアバター」
        /// 「どちらでもなければ null（今と同じ空欄）」の2段だけを見る。純粋関数。
        /// </summary>
        /// <param name="descriptorOwners">シーン内の VRCAvatarDescriptor 所有者すべて（フィルタ前）。</param>
        /// <param name="selected">Hierarchy で選択中のオブジェクト（無ければ null）。</param>
        internal static GameObject PickOutermost(IEnumerable<GameObject> descriptorOwners, GameObject selected)
        {
            var outermost = FindOutermostOwners(descriptorOwners);

            if (outermost.Count == 1)
                return outermost[0];

            if (outermost.Count > 1 && selected != null)
            {
                foreach (var owner in outermost)
                {
                    if (owner == null) continue;
                    if (owner.transform == selected.transform
                        || IsStrictAncestor(owner.transform, selected.transform))
                    {
                        return owner;
                    }
                }
            }

            return null;
        }

        // ───────────────────────────── シーン走査（副作用: Unity シーン参照） ─────────────────────────────

        /// <summary>
        /// <paramref name="scene"/> 内の VRCAvatarDescriptor 所有者（GameObject）をすべて返す（非アクティブ含む）。
        /// <see cref="PickOutermost"/> の入力を作るための薄いラッパー。
        /// </summary>
        internal static List<GameObject> FindSceneDescriptorOwners(Scene scene)
        {
            var descriptors = Object.FindObjectsByType<VRCAvatarDescriptor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var owners = new List<GameObject>();
            foreach (var descriptor in descriptors)
            {
                if (descriptor == null) continue;
                if (descriptor.gameObject.scene != scene) continue;
                owners.Add(descriptor.gameObject);
            }
            return owners;
        }

        // ───────────────────────────── EditorPrefs 永続化（Unity 再起動をまたぐ分・副作用） ─────────────────────────────

        /// <summary>
        /// 対象が決まるたび（手動指定・自動選択どちらも）に呼ぶ。シーンパスごとのキーで
        /// GlobalObjectId 文字列を EditorPrefs に保存する。未保存シーン（path が空）では何もしない。
        /// </summary>
        internal static void SaveLastAvatarRoot(Scene scene, GameObject avatarRoot)
        {
            if (avatarRoot == null) return;

            var key = PrefKeyForScene(scene);
            if (key == null) return;

            var id = GlobalObjectId.GetGlobalObjectIdSlow(avatarRoot);
            EditorPrefs.SetString(key, id.ToString());
        }

        /// <summary>
        /// EditorPrefs からの復元を試みる。<paramref name="scene"/> 内に実在し、かつ
        /// VRCAvatarDescriptor が付いているオブジェクトのときだけ返す。それ以外（キーが無い・
        /// パースできない・シーンから消えている・Descriptor が外れている）は null（呼び出し側は
        /// 「1体だけなら確定」「選択中オブジェクトが属するアバター」へフォールバックする）。
        /// </summary>
        internal static GameObject TryRestoreLastAvatarRoot(Scene scene)
        {
            var key = PrefKeyForScene(scene);
            if (key == null) return null;

            var idString = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(idString)) return null;

            if (!GlobalObjectId.TryParse(idString, out var id)) return null;

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
            if (obj == null) return null;
            if (obj.scene != scene) return null; // 別シーンに同一 GUID のオブジェクトがある場合の誤復元を防ぐ
            if (obj.GetComponent<VRCAvatarDescriptor>() == null) return null;

            return obj;
        }

        /// <summary>シーンパスから EditorPrefs のキーを組み立てる。未保存シーン（path が空）では null。</summary>
        private static string PrefKeyForScene(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path)) return null;
            return LastAvatarRootPrefKeyPrefix + scene.path;
        }
    }
}
