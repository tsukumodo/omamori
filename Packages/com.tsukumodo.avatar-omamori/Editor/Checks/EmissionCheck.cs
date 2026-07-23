using System;
using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEditor;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// アバター配下のマテリアルを走査し、意図せず有効になっている Emission（発光）を検出する。
    /// 「肌にテクスチャを入れたら光り輝くアバターになった」型の事故を、ビルド前に気づけるようにする。
    ///
    /// 「意図的な発光」（光る装飾・目・LED 等）と本質的に区別できないため、
    /// 深刻度は Warning 止まりとし、輝度ゲートで明らかに光っているものだけに絞る（DEC-064）。
    /// </summary>
    public sealed class EmissionCheck : IAvatarCheck
    {
        // CheckResult の予告表示と FixHistoryEntry の実測記録で同じ文字列を共有するため定数化
        private const string ValueLabel = "Emission";

        /// <summary>
        /// 「視認できる発光」とみなす知覚輝度（linear / Rec.709）の下限。
        /// これ未満は、トグルが立っていても実際には光らないとみなしてスキップする。
        ///
        /// 実測較正（2026-07-23 / Unity 2022.3.22f1 / Linear カラースペース / lilToon 2.3.4）:
        ///   - 実アバター 9 プロジェクト・lilToon マテリアル 278 枚を走査した結果、
        ///     _UseEmission=1 は 19 枚。その輝度分布は L=0.0 が 4 枚（色が黒＝光らない）と、
        ///     L=0.2275〜1.7962 が 15 枚（実際に光っている）に完全に二分された。
        ///     0.0 と 0.2275 の間に他のサンプルは無いため、この空白域に閾値を置く。
        ///   - lilToon 空マテリアルの intensity スイープ:
        ///     白 intensity 0（既定色）で L=1.0 / -3 で L=0.125 / -3.5 で L=0.088。
        ///     0.1 は「白の約 1/10 より暗い発光は無視する」に相当する。
        /// 詳細は Tests/Editor/EmissionCheckTests.cs の輝度ゲートのテストを参照。
        /// </summary>
        internal const float LuminanceThreshold = 0.1f;

        /// <summary>ロック済み（Poiyomi/ThryEditor 等）マテリアルのシェーダー名プレフィックス。</summary>
        private const string LockedShaderPrefix = "Hidden/Locked/";

        /// <inheritdoc/>
        public string DisplayName => "[Shader] Emission（意図しない発光）チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => true;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            // 非アクティブなGameObjectも含めて全Rendererを取得
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);

            // 同一マテリアルは複数の Renderer から共有されるため、1件に集約する
            var visited = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;

                foreach (var mat in materials)
                {
                    // null マテリアル・null シェーダーは MissingShaderCheck の責務
                    if (mat == null || mat.shader == null)
                        continue;

                    if (!visited.Add(mat))
                        continue;

                    // ロック済みマテリアルはプロパティが実状態を反映しないため静かにスキップ。
                    // 直せない Warning を乱発しないための分岐（DEC-064）。
                    if (mat.shader.name.Contains(LockedShaderPrefix))
                        continue;

                    var profile = EmissionShaderProfile.Find(mat);
                    if (profile == null)
                        continue;

                    if (!profile.IsEmissionEnabled(mat))
                        continue;

                    float luminance = GetEffectiveLuminance(profile, mat);
                    if (luminance < LuminanceThreshold)
                        continue;

                    // クロージャでキャプチャするため、ループ変数をローカルコピーに取る
                    var capturedMat = mat;
                    var capturedProfile = profile;
                    var path = HierarchyPathUtil.GetHierarchyPath(renderer.gameObject);

                    yield return new CheckResult(
                        Severity.Warning,
                        $"[Shader] {path} のマテリアル \"{mat.name}\" で Emission（発光）が有効です。" +
                            "意図せず光っている場合はオフにできます。光らせたい場合はこのまま無視してOKです。",
                        renderer.gameObject,
                        fixAction: () => DisableEmission(capturedMat, capturedProfile),
                        fixLabel: "発光をオフ",
                        fixConfirmMessage:
                            $"マテリアル「{capturedMat.name}」の Emission をオフにします。\n" +
                            "・このマテリアルを使う他の箇所にも反映されます。\n" +
                            "・メニューで光らせる設定（アニメーションで発光を切り替える設計）の場合は、\n" +
                            "　オフにすると壊れることがあります。心当たりがあればキャンセルしてください。\n" +
                            "Undo（Ctrl+Z）で元に戻せます。",
                        valueLabel: ValueLabel,
                        beforeValue: "ON",
                        afterValue: "OFF"
                    );
                }
            }
        }

        /// <summary>
        /// 実効発光色（発光色 × 強度）の知覚輝度を Rec.709 / linear で求める。
        /// _EmissionMap はサンプルせず、色と強度の「上限エンベロープ」で判定する割り切り。
        /// </summary>
        internal static float GetEffectiveLuminance(EmissionShaderProfile profile, Material mat)
        {
            var color = ToLinear(profile.GetEmissionColor(mat));
            float strength = Mathf.Max(0f, profile.GetStrength(mat));

            float r = color.r * strength;
            float g = color.g * strength;
            float b = color.b * strength;

            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        /// <summary>
        /// マテリアルの色プロパティを linear 空間に揃える。
        /// <see cref="Material.GetColor"/> は .mat に保存された生値をそのまま返すことを実測で確認済み
        /// （2026-07-23 / 実マテリアル 6 枚の YAML 値と突き合わせて不一致 0 件）。
        /// つまり Linear プロジェクトでは linear 値、Gamma プロジェクトではガンマ値が返る。
        /// VRChat の標準は Linear だが、Gamma プロジェクトでも輝度がズレないよう変換を挟む。
        /// </summary>
        private static Color ToLinear(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Gamma ? color.linear : color;
        }

        /// <summary>
        /// マテリアルの Emission トグルをオフにする。
        /// 発光色は残すため、ユーザーが後から同じ色で再度オンにできる。
        /// 修正直前/直後に実値を読んで <see cref="FixHistoryStore"/> に記録する（DEC-055 設計選択C）。
        /// </summary>
        private static void DisableEmission(Material mat, EmissionShaderProfile profile)
        {
            if (mat == null)
                return;

            string before = profile.IsEmissionEnabled(mat) ? "ON" : "OFF";

            Undo.RecordObject(mat, "Fix Emission");
            profile.DisableEmission(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            string after = profile.IsEmissionEnabled(mat) ? "ON" : "OFF";

            FixHistoryStore.Record(new FixHistoryEntry(
                timestamp: DateTime.Now,
                checkName: nameof(EmissionCheck),
                valueLabel: ValueLabel,
                beforeValue: before,
                afterValue: after,
                targetInstanceID: mat.GetInstanceID(),
                targetObjectName: mat.name
            ));

            // 利用統計に修正実行を記録（種別名のみ・opt-out 中は内部で何もしない・DEC-055）
            UsageStatsRecorder.RecordFix(nameof(EmissionCheck));
        }
    }

    /// <summary>
    /// シェーダーごとに異なる Emission の表現方法（トグル・色・強度）を吸収する薄いプロファイル。
    /// v1 の対象は lilToon Main と Unity Standard のみ。
    /// 未対応シェーダーはプロパティ名を推測せず、検出対象外として静かにスキップする。
    /// </summary>
    internal sealed class EmissionShaderProfile
    {
        /// <summary>プロファイルの識別名（ログ・テスト用）。</summary>
        public string Id { get; }

        /// <summary>このプロファイルが対象とするシェーダーか判定する。</summary>
        public Func<Material, bool> Matches { get; }

        /// <summary>Emission トグル／キーワードを読んで有効かどうかを返す。</summary>
        public Func<Material, bool> IsEmissionEnabled { get; }

        /// <summary>発光色（HDR デコード済み）を返す。</summary>
        public Func<Material, Color> GetEmissionColor { get; }

        /// <summary>発光の全体倍率を返す。倍率を持たない系統は 1.0。</summary>
        public Func<Material, float> GetStrength { get; }

        /// <summary>Emission トグルをオフにする（発光色は残す）。</summary>
        public Action<Material> DisableEmission { get; }

        private EmissionShaderProfile(
            string id,
            Func<Material, bool> matches,
            Func<Material, bool> isEmissionEnabled,
            Func<Material, Color> getEmissionColor,
            Func<Material, float> getStrength,
            Action<Material> disableEmission)
        {
            Id = id;
            Matches = matches;
            IsEmissionEnabled = isEmissionEnabled;
            GetEmissionColor = getEmissionColor;
            GetStrength = getStrength;
            DisableEmission = disableEmission;
        }

        /// <summary>
        /// lilToon（Multi/通常版/Lite を含む）。
        /// 有効化ゲートは float の <c>_UseEmission</c>、全体倍率は <c>_EmissionBlend</c>（Lite 等は持たないので既定 1）。
        /// 2nd Emission（<c>_UseEmission2nd</c>）は v1 では扱わない。
        /// </summary>
        public static readonly EmissionShaderProfile LilToon = new EmissionShaderProfile(
            id: "lilToon",
            // 派生バリアントが多いため緩めのマッチにする。実プロジェクトのマテリアルは
            // "Hidden/lilToonOutline" / "Hidden/lilToonTransparent" / "_lil/lilToonMulti" 等の名前で、
            // 素の "lilToon" は少数派だった（実測 278 枚）。前方一致では取りこぼす
            matches: mat => mat.shader.name.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0
                            && mat.HasProperty("_UseEmission"),
            isEmissionEnabled: mat => mat.GetFloat("_UseEmission") > 0.5f,
            getEmissionColor: mat => mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
            getStrength: mat => mat.HasProperty("_EmissionBlend") ? mat.GetFloat("_EmissionBlend") : 1f,
            disableEmission: mat => mat.SetFloat("_UseEmission", 0f)
        );

        /// <summary>
        /// Unity Standard / Standard (Specular setup)。
        /// 有効化ゲートは float ではなく <c>_EMISSION</c> キーワード。
        /// オフにするときはライトマップ用の GI フラグからも Emissive を落とす（Unity 標準 GUI と同じ手順）。
        /// </summary>
        public static readonly EmissionShaderProfile Standard = new EmissionShaderProfile(
            id: "Standard",
            matches: mat => mat.shader.name == "Standard" || mat.shader.name == "Standard (Specular setup)",
            isEmissionEnabled: mat => mat.IsKeywordEnabled("_EMISSION"),
            getEmissionColor: mat => mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
            // Standard は倍率プロパティを持たず、強度は色の HDR intensity に内包される
            getStrength: mat => 1f,
            disableEmission: mat =>
            {
                mat.DisableKeyword("_EMISSION");

                // Unity の StandardShaderGUI と同じ手順で GI フラグを更新する。
                // Baked/Realtime のいずれかが立っているときだけ EmissiveIsBlack を立て、
                // 「GI に寄与しない」状態に戻す。
                var flags = mat.globalIlluminationFlags;
                if ((flags & (MaterialGlobalIlluminationFlags.BakedEmissive | MaterialGlobalIlluminationFlags.RealtimeEmissive)) != 0)
                {
                    flags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    mat.globalIlluminationFlags = flags;
                }
            }
        );

        /// <summary>v1 で対応するプロファイルの一覧。上から順にマッチを試す。</summary>
        private static readonly EmissionShaderProfile[] All = { LilToon, Standard };

        /// <summary>
        /// マテリアルに対応するプロファイルを返す。未対応シェーダーなら null。
        /// </summary>
        public static EmissionShaderProfile Find(Material mat)
        {
            if (mat == null || mat.shader == null)
                return null;

            foreach (var profile in All)
            {
                if (profile.Matches(mat))
                    return profile;
            }

            return null;
        }
    }
}
