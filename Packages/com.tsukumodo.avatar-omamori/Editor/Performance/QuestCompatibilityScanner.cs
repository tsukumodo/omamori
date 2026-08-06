using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// 検出した非対応要素が、どのプラットフォームで問題になるか。
    /// PC でも剥がされるものを「Quest では使えないもの」の中に並べると、
    /// PC しか使わないユーザーが「自分には関係ない」と読み飛ばしてしまうため区別する。
    /// </summary>
    public enum IncompatibilityScope
    {
        /// <summary>Quest / iOS でのみ問題になる。</summary>
        Quest,

        /// <summary>PC / Quest を問わず、アップロード時に取り除かれる。</summary>
        AllPlatforms
    }

    /// <summary>
    /// アップロード時に問題になる要素1件分（層A）。
    /// 「なぜ見た目が変わる・動かないのか」を1行で説明し、対象を Hierarchy で選べるようにする。
    /// </summary>
    public sealed class QuestIncompatibility
    {
        /// <summary>見出し（例: "Quest では使えないシェーダー"）。</summary>
        public string Label { get; }

        /// <summary>件数。</summary>
        public int Count { get; }

        /// <summary>内訳や影響の説明（例: "Hidden/lilToonOutline ×35 / … — Quest では…"）。</summary>
        public string Detail { get; }

        /// <summary>「対象を選択」で Hierarchy 選択する対象。空なら選択ボタンを出さない。</summary>
        public IReadOnlyList<UnityEngine.Object> Targets { get; }

        /// <summary>どのプラットフォームで問題になるか。UI の見出し分けに使う。</summary>
        public IncompatibilityScope Scope { get; }

        /// <summary>「調べる」で開く公式ドキュメント。適切な案内先が無い場合は null（ボタンを出さない）。</summary>
        public string DocumentUrl { get; }

        public QuestIncompatibility(
            string label,
            int count,
            string detail,
            IReadOnlyList<UnityEngine.Object> targets,
            IncompatibilityScope scope = IncompatibilityScope.Quest,
            string documentUrl = null)
        {
            Label = label;
            Count = count;
            Detail = detail;
            Targets = targets ?? Array.Empty<UnityEngine.Object>();
            Scope = scope;
            DocumentUrl = documentUrl ?? (scope == IncompatibilityScope.Quest
                ? PerformanceCategoryLabels.QuestDocUrl
                : null);
        }
    }

    /// <summary>
    /// Quest / iOS で「弾かれる・剥がされる」要素（層A）を検出する。
    ///
    /// <para>
    /// シェーダーは SDK の <c>VRC.SDK3.Validation.AvatarValidation.FindIllegalShaders()</c> に委譲する。
    /// ホワイトリスト（<c>ShaderWhiteList</c>）に <c>#if</c> が無いため、PC ターゲットのままでも
    /// Quest 用の判定が得られる（T-1 実機検証で確認済み）。
    /// </para>
    /// <para>
    /// ⚠ コンポーネントは SDK に委譲できない。SDK の <c>ComponentTypeWhiteListCommon</c> は
    /// Quest で禁止される型（Light / Camera / AudioSource / Cloth / 物理 / Unity Constraints 等）を
    /// <c>#if UNITY_STANDALONE</c> で囲んでおり、PC ターゲットのエディタでは「合法」としてコンパイルされる。
    /// 実測でも PC ターゲットでは <c>FindIllegalComponents</c> が 0 件を返した。
    /// このため下記の自前リストで判定する。<b>SDK 更新で禁止対象が変わる可能性があるため、
    /// SDK のバージョンを上げたときは AvatarValidation.cs の #if UNITY_STANDALONE ブロックと突き合わせること。</b>
    /// </para>
    /// </summary>
    internal static class QuestCompatibilityScanner
    {
        /// <summary>
        /// Quest / iOS では無効化される（＝ビルド時に剥がされる）コンポーネント。
        /// 出典: VRCSDK の <c>VRC.SDKBase.Validation.AvatarValidation.ComponentTypeWhiteListCommon</c>
        /// および <c>ComponentTypeWhiteListSdk3</c> の <c>#if UNITY_STANDALONE</c> ブロック（SDK 3.10.3 時点）。
        /// Unity Constraint は移行先が明確で案内の価値が高いため、別項目として分離している。
        /// </summary>
        private static readonly (Type Type, string Label)[] MobileForbiddenComponents =
        {
            (typeof(Light), "Light"),
            (typeof(Camera), "Camera"),
            (typeof(AudioSource), "Audio Source"),
            (typeof(Cloth), "Cloth"),
            (typeof(Collider), "Collider（物理）"),
            (typeof(Rigidbody), "Rigidbody"),
            (typeof(Joint), "Joint"),
        };

        /// <summary>
        /// アバター配下を走査し、Quest / iOS で問題になる要素を列挙する。
        /// 検出ゼロなら空のリストを返す。現在のビルドターゲットから mobile 判定を行う。
        /// </summary>
        public static List<QuestIncompatibility> Scan(GameObject avatarRoot)
        {
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            var isMobileBuildTarget = activeBuildTarget == BuildTarget.Android || activeBuildTarget == BuildTarget.iOS;
            return Scan(avatarRoot, isMobileBuildTarget);
        }

        /// <summary>
        /// <see cref="Scan(GameObject)"/> の実処理。ビルドターゲット判定をテストから固定できるよう分離している。
        /// </summary>
        internal static List<QuestIncompatibility> Scan(GameObject avatarRoot, bool isMobileBuildTarget)
        {
            var results = new List<QuestIncompatibility>();
            if (avatarRoot == null) return results;

            // 同じコンポーネントを2つの項目で数えないようにする。
            // ⚠ 「どちらを先に reported へ登録するか」はビルドターゲットで入れ替える（片方を止めるのではない）。
            //    - Standalone ターゲット: SDK の FindIllegalComponents は #if UNITY_STANDALONE の外側では
            //      「PC でも剥がされるもの」しか返さない。これは常に正しいので先に登録し、自前リスト側から除外する。
            //    - Android / iOS ターゲット: SDK は Light / Camera / AudioSource / Cloth / Collider / Rigidbody /
            //      Joint / Unity Constraint まで含めて「非対応」を返してくる。これを先に AllPlatforms として
            //      登録すると、本来 PC では動く要素まで「PC / Quest を問わず取り除かれます」という誤った説明になり、
            //      Unity Constraint の置き換え案内（自前リスト側の価値）も消えてしまう。
            //      そのため自前リストを先に登録し、SDK 側の結果はその残り（＝本当に PC でも剥がされるもの）だけを見る
            //    - 副作用として、Android / iOS ターゲットでは MeshCollider のような「自前リストの
            //      Collider（物理）にも該当し、かつ PC でも剥がされる」型が「（PC では動作します）」と
            //      誤って説明される。これは許容する: Android ターゲットでは SDK の Standalone 用
            //      ホワイトリストがそもそもコンパイルされず「PC でも非対応か」を判定する術が無く、
            //      影響も実質 MeshCollider 系のみ。Light / Camera / Audio Source というアバターで遥かに
            //      一般的な要素の誤説明（入れ替えない場合の弊害）のほうが実害が大きい。正すには SDK の
            //      PC 側ホワイトリストを自前で持つ必要があり、「自前の判定表を持たない」方針（DEC-069 決定事項8）に反する
            var reported = new HashSet<Component>();

            AddIllegalShaders(avatarRoot, results);

            if (isMobileBuildTarget)
            {
                AddForbiddenComponents(avatarRoot, results, reported);
                AddUnityConstraints(avatarRoot, results, reported);
                AddSdkIllegalComponents(avatarRoot, results, reported);
            }
            else
            {
                AddSdkIllegalComponents(avatarRoot, results, reported);
                AddForbiddenComponents(avatarRoot, results, reported);
                AddUnityConstraints(avatarRoot, results, reported);
            }

            return results;
        }

        private static void AddIllegalShaders(GameObject avatarRoot, List<QuestIncompatibility> results)
        {
            HashSet<Shader> illegalShaders;
            try
            {
                illegalShaders = new HashSet<Shader>(
                    VRC.SDK3.Validation.AvatarValidation.FindIllegalShaders(avatarRoot).Where(s => s != null));
            }
            catch (Exception e)
            {
                // SDK 側の変更で呼べなくなっても、他の検出は続ける
                Debug.LogWarning($"[AvatarOmamori] Quest 非対応シェーダーの判定に失敗しました。{e.Message}");
                return;
            }

            if (illegalShaders.Count == 0) return;

            // どの Renderer が該当シェーダーを使っているかを引き当てて、選択できるようにする。
            // あわせてシェーダーごとの使用マテリアル数を数え、影響の大きいものから並べる
            // （illegalShaders は集合なので、シェーダー自体を GroupBy しても件数は常に1になり並べ替えの意味がない）
            var targets = new List<UnityEngine.Object>();
            var materialCountByShader = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                var used = false;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) continue;
                    if (!illegalShaders.Contains(material.shader)) continue;

                    materialCountByShader.TryGetValue(material.shader.name, out var count);
                    materialCountByShader[material.shader.name] = count + 1;
                    used = true;
                }

                if (used) targets.Add(renderer.gameObject);
            }

            var detail = string.Join(" / ", materialCountByShader
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(3)
                .Select(pair => $"{pair.Key} ×{pair.Value}"));
            if (materialCountByShader.Count > 3) detail += " ほか";
            if (detail.Length == 0)
            {
                // Renderer 以外（アニメーションで差し替わるマテリアル等）にしか無い場合の保険
                detail = string.Join(" / ", illegalShaders.Select(s => s.name).OrderBy(n => n, StringComparer.Ordinal).Take(3));
            }

            results.Add(new QuestIncompatibility(
                "Quest では使えないシェーダー",
                illegalShaders.Count,
                $"{detail} — Quest では VRChat/Mobile 系のシェーダーに置き換わり、見た目が変わります",
                targets));
        }

        private static void AddForbiddenComponents(
            GameObject avatarRoot, List<QuestIncompatibility> results, HashSet<Component> reported)
        {
            foreach (var (type, label) in MobileForbiddenComponents)
            {
                // Standalone ターゲットでは、このメソッドより先に SDK 判定（AddSdkIllegalComponents）が走る。
                // SDK が「PC でも非対応」と判定済みのものはそちらの項目に任せる。
                // ここの文言は「PC では動作します」で固定なので、混ぜると誤った説明になる
                var found = avatarRoot.GetComponentsInChildren(type, true)
                    .Where(c => !reported.Contains(c))
                    .ToList();
                if (found.Count == 0) continue;

                foreach (var component in found) reported.Add(component);

                results.Add(new QuestIncompatibility(
                    $"Quest では無効になる {label}",
                    found.Count,
                    "Quest / iOS ではビルド時に取り除かれます（PC では動作します）",
                    found.Select(c => (UnityEngine.Object)c.gameObject).ToList()));
            }
        }

        private static void AddUnityConstraints(
            GameObject avatarRoot, List<QuestIncompatibility> results, HashSet<Component> reported)
        {
            // Unity 標準の Constraint（IConstraint）は Quest で無効。VRChat Constraints への置き換えが必要。
            var components = avatarRoot.GetComponentsInChildren<IConstraint>(true)
                .OfType<Component>()
                .Where(c => !reported.Contains(c))
                .ToList();
            if (components.Count == 0) return;

            foreach (var component in components) reported.Add(component);

            results.Add(new QuestIncompatibility(
                "Unity の Constraint",
                components.Count,
                "Quest / iOS では動きません。VRChat Constraints への置き換えをおすすめします",
                components.Select(c => (UnityEngine.Object)c.gameObject).ToList(),
                // Detail は VRChat Constraints への置き換えを案内しているので、
                // リンク先も Quest の制限ページ（既定値）ではなく Constraints の解説ページに揃える
                documentUrl: PerformanceCategoryLabels.ConstraintDocUrl));
        }

        /// <summary>
        /// 現在のビルドターゲットで SDK 自身が「非対応」と判定するコンポーネント。
        ///
        /// <para>
        /// Standalone ターゲットで検出されるのは DynamicBone / MeshCollider など、<b>PC でも剥がされるもの</b>。
        /// Quest 固有の話ではないので <see cref="IncompatibilityScope.AllPlatforms"/> として報告し、
        /// UI 側でも「Quest では使えないもの」とは別の見出しに置く。
        /// </para>
        /// <para>
        /// Android / iOS ターゲットでは、このメソッドより先に自前リスト（<see cref="AddForbiddenComponents"/> /
        /// <see cref="AddUnityConstraints"/>）が走り、<paramref name="reported"/> に登録済みになっている。
        /// SDK がこのターゲットで返す集合には Light / Camera など「PC では動く」ものまで含まれるため、
        /// <paramref name="reported"/> でフィルタして残りだけを AllPlatforms として報告する。
        /// </para>
        /// </summary>
        private static void AddSdkIllegalComponents(
            GameObject avatarRoot, List<QuestIncompatibility> results, HashSet<Component> reported)
        {
            List<Component> illegal;
            try
            {
                illegal = VRC.SDK3.Validation.AvatarValidation
                    .FindIllegalComponents(avatarRoot)
                    .Where(c => c != null)
                    .Distinct()
                    .Where(c => !reported.Contains(c))
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] 非対応コンポーネントの判定に失敗しました。{e.Message}");
                return;
            }

            if (illegal.Count == 0) return;

            // ここで報告したものは登録しておく（Standalone ターゲットではこのあとに自前リストが走るため、
            // 二重報告を防ぐ。Android / iOS ターゲットではこのメソッドが最後に走るので実質意味を持たない）
            foreach (var component in illegal) reported.Add(component);

            var detail = string.Join(" / ", illegal
                .GroupBy(c => c.GetType().Name)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Take(3)
                .Select(g => $"{g.Key} ×{g.Count()}"));

            results.Add(new QuestIncompatibility(
                "VRChat が対応していないコンポーネント",
                illegal.Count,
                $"{detail} — PC / Quest を問わず、アップロード時に取り除かれます",
                illegal.Select(c => (UnityEngine.Object)c.gameObject).Distinct().ToList(),
                IncompatibilityScope.AllPlatforms));
        }
    }
}
