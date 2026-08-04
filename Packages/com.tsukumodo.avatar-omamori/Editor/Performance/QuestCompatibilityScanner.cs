using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;

namespace AvatarOmamori.Editor.Performance
{
    /// <summary>
    /// Quest / iOS で使えない要素1件分（層A）。
    /// 「なぜ Quest だと見た目が変わる・動かないのか」を1行で説明し、対象を Hierarchy で選べるようにする。
    /// </summary>
    public sealed class QuestIncompatibility
    {
        /// <summary>見出し（例: "Quest では使えないシェーダー"）。</summary>
        public string Label { get; }

        /// <summary>件数。</summary>
        public int Count { get; }

        /// <summary>内訳や影響の説明（例: "Hidden/lilToonOutline / Hidden/lilToonTransparent ほか — Quest では…"）。</summary>
        public string Detail { get; }

        /// <summary>「対象を選択」で Hierarchy 選択する対象。空なら選択ボタンを出さない。</summary>
        public IReadOnlyList<UnityEngine.Object> Targets { get; }

        public QuestIncompatibility(string label, int count, string detail, IReadOnlyList<UnityEngine.Object> targets)
        {
            Label = label;
            Count = count;
            Detail = detail;
            Targets = targets ?? Array.Empty<UnityEngine.Object>();
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
        /// 検出ゼロなら空のリストを返す。
        /// </summary>
        public static List<QuestIncompatibility> Scan(GameObject avatarRoot)
        {
            var results = new List<QuestIncompatibility>();
            if (avatarRoot == null) return results;

            AddIllegalShaders(avatarRoot, results);
            AddForbiddenComponents(avatarRoot, results);
            AddUnityConstraints(avatarRoot, results);
            AddSdkIllegalComponents(avatarRoot, results);

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

        private static void AddForbiddenComponents(GameObject avatarRoot, List<QuestIncompatibility> results)
        {
            foreach (var (type, label) in MobileForbiddenComponents)
            {
                var found = avatarRoot.GetComponentsInChildren(type, true);
                if (found.Length == 0) continue;

                results.Add(new QuestIncompatibility(
                    $"Quest では無効になる {label}",
                    found.Length,
                    "Quest / iOS ではビルド時に取り除かれます（PC では動作します）",
                    found.Select(c => (UnityEngine.Object)c.gameObject).ToList()));
            }
        }

        private static void AddUnityConstraints(GameObject avatarRoot, List<QuestIncompatibility> results)
        {
            // Unity 標準の Constraint（IConstraint）は Quest で無効。VRChat Constraints への置き換えが必要。
            var constraints = avatarRoot.GetComponentsInChildren<IConstraint>(true);
            if (constraints.Length == 0) return;

            results.Add(new QuestIncompatibility(
                "Unity の Constraint",
                constraints.Length,
                "Quest / iOS では動きません。VRChat Constraints への置き換えをおすすめします",
                constraints.OfType<Component>().Select(c => (UnityEngine.Object)c.gameObject).ToList()));
        }

        private static void AddSdkIllegalComponents(GameObject avatarRoot, List<QuestIncompatibility> results)
        {
            // 現在のビルドターゲットで SDK 自身が「非対応」と判定するコンポーネント。
            // PC ターゲットで検出されるのは DynamicBone / MeshCollider など、PC でも剥がされるものだけになる。
            List<Component> illegal;
            try
            {
                illegal = VRC.SDK3.Validation.AvatarValidation
                    .FindIllegalComponents(avatarRoot)
                    .Where(c => c != null)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] 非対応コンポーネントの判定に失敗しました。{e.Message}");
                return;
            }

            if (illegal.Count == 0) return;

            var detail = string.Join(" / ", illegal
                .GroupBy(c => c.GetType().Name)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key} {g.Count()}"));

            results.Add(new QuestIncompatibility(
                "VRChat が対応していないコンポーネント",
                illegal.Count,
                $"{detail} — アップロード時に取り除かれます",
                illegal.Select(c => (UnityEngine.Object)c.gameObject).Distinct().ToList()));
        }
    }
}
