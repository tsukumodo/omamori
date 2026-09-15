using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// 内訳の「まとまり」1件。後から足した服・装飾品ひとかたまり、またはアバター本体。
    /// </summary>
    public sealed class AvatarPartGroup
    {
        /// <summary>見出しに出す名前。アバター本体は <see cref="AvatarPartGrouping.AvatarBodyName"/>。</summary>
        public string Name { get; }

        /// <summary>まとまりの一番外側の GameObject。アバター本体のまとまりは null（外せる実体が無いため）。</summary>
        public GameObject Root { get; }

        /// <summary>アバター本体（外す対象ではない）かどうか。並びでは常に最後に置く。</summary>
        public bool IsAvatarBody { get; }

        /// <summary>このまとまりに属する Renderer。</summary>
        public IReadOnlyList<Renderer> Renderers { get; }

        public AvatarPartGroup(string name, GameObject root, bool isAvatarBody, IReadOnlyList<Renderer> renderers)
        {
            Name = name;
            Root = root;
            IsAvatarBody = isAvatarBody;
            Renderers = renderers ?? new List<Renderer>();
        }
    }

    /// <summary>
    /// プレハブ由来の判定だけを切り出した境界。
    /// 本番では <see cref="PrefabUtilityStructure"/> が <c>PrefabUtility</c> をそのまま呼ぶ。
    /// テストからは差し替えて、プレハブ資産を用意せずにルール1〜6を検証する。
    /// </summary>
    internal interface IPrefabStructure
    {
        /// <summary>プレハブインスタンスの一部か（false なら Unpack 済み）。</summary>
        bool IsPrefabInstance(GameObject go);

        /// <summary>プレハブに対して「後から足された」オブジェクトか。</summary>
        bool IsAddedObject(GameObject go);

        /// <summary>自分自身がプレハブインスタンスの一番外側か。</summary>
        bool IsPrefabInstanceRoot(GameObject go);
    }

    internal sealed class PrefabUtilityStructure : IPrefabStructure
    {
        public static readonly PrefabUtilityStructure Instance = new PrefabUtilityStructure();

        public bool IsPrefabInstance(GameObject go)
        {
            return go != null && PrefabUtility.IsPartOfPrefabInstance(go);
        }

        public bool IsAddedObject(GameObject go)
        {
            return go != null && PrefabUtility.IsAddedGameObjectOverride(go);
        }

        public bool IsPrefabInstanceRoot(GameObject go)
        {
            return go != null && PrefabUtility.GetNearestPrefabInstanceRoot(go) == go;
        }
    }

    /// <summary>
    /// Renderer を「服・装飾品ごと」のまとまりに振り分ける（v0.11.0 W0 設計 §11.3 / DEC-109）。
    ///
    /// <para>
    /// T-8 実機確認で、Renderer 単位だと衣装の17パーツが全部「消しても減りません」になり、
    /// 外すと 64.5MB 減る一番重い服が一番軽く見えることが分かった。絵柄を衣装の中で使い回しているため。
    /// まとまりで見れば「他の服にしよう」に答えられる。
    /// </para>
    /// <para>
    /// ルール（§11.3）:
    /// 1 後から足したプレハブの一番外側＝1まとまり（置き場所は問わない）
    /// 2 後から足した空の入れ物（自分はメッシュを持たない非プレハブ）はまとまりにせず中に降りる
    /// 3 後から足した非プレハブで自分がメッシュを持つものは1まとまり
    /// 4 メッシュを1つも持たないまとまりは出さない
    /// 5 それ以外は「アバター本体」
    /// 6 アバターが Unpack 済みのときは直下の子ごとにまとまりを作り「アバター本体」の見出しを出さない
    /// </para>
    /// </summary>
    internal static class AvatarPartGrouping
    {
        /// <summary>アバター本体のまとまりの見出し。</summary>
        public const string AvatarBodyName = "アバター本体";

        public static IReadOnlyList<AvatarPartGroup> Build(GameObject avatarRoot, IReadOnlyList<Renderer> renderers)
        {
            return Build(avatarRoot, renderers, PrefabUtilityStructure.Instance);
        }

        internal static IReadOnlyList<AvatarPartGroup> Build(
            GameObject avatarRoot, IReadOnlyList<Renderer> renderers, IPrefabStructure prefab)
        {
            var groups = new List<AvatarPartGroup>();
            if (avatarRoot == null || renderers == null || renderers.Count == 0) return groups;

            // ルール6: Unpack 済みだと本体と追加物を区別できないので、直下の子ごとに切る
            var unpacked = !prefab.IsPrefabInstance(avatarRoot);

            var groupRoots = new List<GameObject>();
            if (unpacked)
            {
                foreach (Transform child in avatarRoot.transform) groupRoots.Add(child.gameObject);
            }
            else
            {
                CollectGroupRoots(avatarRoot.transform, insideAddedObject: false, prefab: prefab, into: groupRoots);
            }

            var buckets = new Dictionary<GameObject, List<Renderer>>();
            foreach (var groupRoot in groupRoots)
            {
                if (!buckets.ContainsKey(groupRoot)) buckets[groupRoot] = new List<Renderer>();
            }

            var leftovers = new List<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var owner = FindOwner(renderer.transform, buckets);
                if (owner != null) buckets[owner].Add(renderer);
                else leftovers.Add(renderer);
            }

            foreach (var groupRoot in groupRoots)
            {
                var members = buckets[groupRoot];
                if (members.Count == 0) continue; // ルール4: メッシュを持たないまとまりは出さない
                groups.Add(new AvatarPartGroup(groupRoot.name, groupRoot, isAvatarBody: false, renderers: members));
            }

            if (leftovers.Count > 0)
            {
                // ルール5: 元からアバターに含まれていたもの。
                // Unpack 済み（ルール6）のときは「アバター本体」の見出しを出さないが、
                // アバター直下に直接付いている Renderer を落とさないよう、ルート名のまとまりとして出す。
                groups.Add(unpacked
                    ? new AvatarPartGroup(avatarRoot.name, avatarRoot, isAvatarBody: false, renderers: leftovers)
                    : new AvatarPartGroup(AvatarBodyName, null, isAvatarBody: true, renderers: leftovers));
            }

            return groups;
        }

        /// <summary>
        /// まとまりの一番外側になる GameObject を集める。
        /// <paramref name="insideAddedObject"/> は「後から足した入れ物の中を降りている最中」かどうか。
        /// 入れ物の子は <c>IsAddedGameObjectOverride</c> が false になる（追加判定は一番外側にしか付かない）ため、
        /// フラグで引き継ぐ必要がある。
        /// </summary>
        private static void CollectGroupRoots(
            Transform parent, bool insideAddedObject, IPrefabStructure prefab, List<GameObject> into)
        {
            foreach (Transform child in parent)
            {
                var go = child.gameObject;
                var added = insideAddedObject || prefab.IsAddedObject(go);

                if (!added)
                {
                    // 元からある枝。骨の下などに後から足したものがあるので、そのまま降りて探す
                    CollectGroupRoots(child, insideAddedObject: false, prefab: prefab, into: into);
                    continue;
                }

                if (prefab.IsPrefabInstanceRoot(go))
                {
                    into.Add(go); // ルール1
                    continue;
                }

                if (go.GetComponent<Renderer>() != null)
                {
                    into.Add(go); // ルール3
                    continue;
                }

                // ルール2: 空の入れ物。まとまりにせず中身を1件ずつ見る
                CollectGroupRoots(child, insideAddedObject: true, prefab: prefab, into: into);
            }
        }

        /// <summary>
        /// Renderer から祖先を辿り、最初に当たったまとまり（＝いちばん深いまとまり）を返す。
        /// どのまとまりにも属さなければ null。
        /// </summary>
        private static GameObject FindOwner(Transform t, Dictionary<GameObject, List<Renderer>> buckets)
        {
            while (t != null)
            {
                if (buckets.ContainsKey(t.gameObject)) return t.gameObject;
                t = t.parent;
            }

            return null;
        }
    }
}
