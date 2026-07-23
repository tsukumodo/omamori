using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class EmissionCheckTests
    {
        private string _statsDir;
        private GameObject _root;
        private readonly List<Object> _disposables = new List<Object>();
        private readonly EmissionCheck _check = new EmissionCheck();

        [SetUp]
        public void SetUp()
        {
            // FixAction が利用統計を記録するため、保存先を一時フォルダへ退避する
            _statsDir = UsageStatsTestUtil.BeginOverride();
            FixHistoryStore.Clear();
            _root = new GameObject("Avatar");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            UsageStatsTestUtil.EndOverride(_statsDir);
            FixHistoryStore.Clear();
            foreach (var obj in _disposables)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _disposables.Clear();
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Renderer BodyRenderer => _root.GetComponentInChildren<Renderer>();

        /// <summary>後始末対象に積んだうえで返す。</summary>
        private T Track<T>(T obj) where T : Object
        {
            _disposables.Add(obj);
            return obj;
        }

        // ------------------------------------------------------------------
        // シェーダーの用意
        // ------------------------------------------------------------------

        /// <summary>
        /// lilToon 相当のシェーダーを返す。lilToon が入っていないプロジェクト（CI・本リポジトリ）では
        /// 同名・同プロパティのスタブを生成して代用する。
        /// </summary>
        private Shader GetLilToonShader()
        {
            var installed = Shader.Find("lilToon");
            if (installed != null)
                return installed;

            return Track(CreateStubShader("lilToon"));
        }

        /// <summary>
        /// lilToon と同じ Emission プロパティ群を持つスタブシェーダーを、指定した名前で生成する。
        /// </summary>
        private static Shader CreateStubShader(string shaderName)
        {
            var source = $@"
Shader ""{shaderName}""
{{
    Properties
    {{
        _UseEmission (""Use Emission"", Float) = 0
        [HDR] _EmissionColor (""Emission Color"", Color) = (1,1,1,1)
        _EmissionBlend (""Emission Blend"", Float) = 1
    }}
    SubShader
    {{
        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""
            float4 vert(float4 v : POSITION) : SV_POSITION {{ return UnityObjectToClipPos(v); }}
            fixed4 frag() : SV_Target {{ return fixed4(1,1,1,1); }}
            ENDCG
        }}
    }}
}}";
            return ShaderUtil.CreateShaderAsset(source, false);
        }

        /// <summary>
        /// lilToon 相当のマテリアルを作って Body に設定する。
        /// </summary>
        private Material SetupLilToonMaterial(float useEmission, Color emissionColor, float blend = 1f)
        {
            var mat = Track(new Material(GetLilToonShader())) as Material;
            mat.name = "TestSkin";
            mat.SetFloat("_UseEmission", useEmission);
            mat.SetColor("_EmissionColor", emissionColor);
            if (mat.HasProperty("_EmissionBlend"))
                mat.SetFloat("_EmissionBlend", blend);
            BodyRenderer.sharedMaterials = new[] { mat };
            return mat;
        }

        /// <summary>
        /// Standard マテリアルを作って Body に設定する。
        /// </summary>
        private Material SetupStandardMaterial(bool emissionEnabled, Color emissionColor)
        {
            var mat = Track(new Material(Shader.Find("Standard"))) as Material;
            mat.name = "TestStandard";
            mat.SetColor("_EmissionColor", emissionColor);
            if (emissionEnabled)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }
            BodyRenderer.sharedMaterials = new[] { mat };
            return mat;
        }

        // ------------------------------------------------------------------
        // lilToon
        // ------------------------------------------------------------------

        [Test]
        public void lilToonで発光が有効なら警告し修正でオフになる()
        {
            // @M8usM 型（肌マテリアルに明るい発光色）の最小再現
            var mat = SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.white);

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            var result = results[0];
            Assert.AreEqual(Severity.Warning, result.Severity);
            Assert.That(result.Message, Does.Contain("TestSkin"));
            Assert.IsTrue(result.HasFix);
            Assert.AreEqual("発光をオフ", result.FixLabel);
            Assert.AreEqual("Emission", result.ValueLabel);
            Assert.AreEqual("ON", result.BeforeValue);
            Assert.AreEqual("OFF", result.AfterValue);
            // アニメトグルで壊れうる旨の警告は確認文の必須要素（DEC-064 §4-6 の唯一の緩和策）
            Assert.That(result.FixConfirmMessage, Does.Contain("アニメーション"));
            Assert.That(result.FixConfirmMessage, Does.Contain("他の箇所"));

            result.FixAction();

            Assert.AreEqual(0f, mat.GetFloat("_UseEmission"), "修正後も _UseEmission が 1 のまま");
            Assert.AreEqual(Color.white, mat.GetColor("_EmissionColor"), "発光色は残すべき（再度オンにできるように）");
            Assert.IsEmpty(_check.Execute(_root).ToList());
            Assert.AreEqual(1, FixHistoryStore.Count, "修正履歴が記録されていない");
        }

        [Test]
        public void lilToonで発光がオフなら検出しない()
        {
            SetupLilToonMaterial(useEmission: 0f, emissionColor: Color.white);

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void lilToonで発光色が黒なら検出しない()
        {
            // トグルは立っているが色が黒＝実際には光らないので誤爆ではない
            SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.black);

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void lilToonで強度が0なら検出しない()
        {
            // _EmissionBlend=0 は実効発光がゼロ
            var mat = SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.white, blend: 0f);

            if (!mat.HasProperty("_EmissionBlend"))
                Assert.Ignore("このシェーダーは _EmissionBlend を持たない（lilToon Lite 相当）");

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        // ------------------------------------------------------------------
        // Standard
        // ------------------------------------------------------------------

        [Test]
        public void Standardで発光キーワードが有効なら警告し修正でオフになる()
        {
            var mat = SetupStandardMaterial(emissionEnabled: true, emissionColor: Color.white);

            var results = _check.Execute(_root).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Warning, results[0].Severity);
            Assert.IsTrue(results[0].HasFix);

            results[0].FixAction();

            Assert.IsFalse(mat.IsKeywordEnabled("_EMISSION"), "修正後も _EMISSION キーワードが有効なまま");
            Assert.IsTrue(
                (mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) != 0,
                "GI フラグから Emissive が落ちていない");
            Assert.IsEmpty(_check.Execute(_root).ToList());
            Assert.AreEqual(1, FixHistoryStore.Count, "修正履歴が記録されていない");
        }

        [Test]
        public void Standardの修正でRealtimeとBakedのGIフラグが残らない()
        {
            // Unity は Standard の _EMISSION キーワードを GI フラグから再導出するため、
            // Realtime/Baked ビットが残っていると保存・再インポートのたびに発光が復活する。
            // Komano 実機で「修正しても光が消えない」不具合として顕在化した（2026-07-23）。
            // EmissiveIsBlack ビットの有無だけを見るアサーションでは素通りするので、
            // 「Realtime/Baked が落ちていること」を独立に固定する。
            var mat = SetupStandardMaterial(emissionEnabled: true, emissionColor: Color.white);

            _check.Execute(_root).ToList()[0].FixAction();

            Assert.AreEqual(
                MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                mat.globalIlluminationFlags,
                "GI フラグに Realtime/Baked が残っている（Unity がキーワードを再有効化してしまう）");
        }

        [Test]
        public void Standardの修正でも発光色は保持される()
        {
            // 「トグルだけオフにして色は残す」方針（DEC-064）が Standard でも成立していること。
            var color = new Color(1f, 0.85f, 0.4f, 1f);
            var mat = SetupStandardMaterial(emissionEnabled: true, emissionColor: color);

            _check.Execute(_root).ToList()[0].FixAction();

            // Color の厳密比較はマテリアル往復での丸め差で落ちるため、成分ごとに許容誤差で見る
            var actual = mat.GetColor("_EmissionColor");
            Assert.AreEqual(color.r, actual.r, 1e-4f, "修正で発光色(R)が変わっている");
            Assert.AreEqual(color.g, actual.g, 1e-4f, "修正で発光色(G)が変わっている");
            Assert.AreEqual(color.b, actual.b, 1e-4f, "修正で発光色(B)が変わっている");
        }

        [Test]
        public void Standardで発光キーワードが無効なら検出しない()
        {
            SetupStandardMaterial(emissionEnabled: false, emissionColor: Color.white);

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void Standardで発光色が黒なら検出しない()
        {
            SetupStandardMaterial(emissionEnabled: true, emissionColor: Color.black);

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        // ------------------------------------------------------------------
        // スキップ条件
        // ------------------------------------------------------------------

        [Test]
        public void 未対応シェーダーは検出しない()
        {
            // プロパティ名を推測して誤読するより、静かにスキップする方針
            var mat = Track(new Material(Shader.Find("Unlit/Color"))) as Material;
            mat.name = "Unsupported";
            BodyRenderer.sharedMaterials = new[] { mat };

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void ロック済みマテリアルは検出しない()
        {
            // Poiyomi/ThryEditor でロックすると Hidden/Locked/... に差し替わり、
            // プロパティ値が実状態を反映しなくなる。直せない Warning を出さないため静かにスキップする
            var locked = Track(CreateStubShader("Hidden/Locked/lilToon/0123456789abcdef")) as Shader;
            var mat = Track(new Material(locked)) as Material;
            mat.name = "LockedMat";
            mat.SetFloat("_UseEmission", 1f);
            mat.SetColor("_EmissionColor", Color.white);
            BodyRenderer.sharedMaterials = new[] { mat };

            Assert.IsEmpty(_check.Execute(_root).ToList());
        }

        [Test]
        public void 共有マテリアルは1件に集約する()
        {
            var mat = SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.white);

            // 同じマテリアルを別 Renderer と同一 Renderer の別スロットの両方から参照させる
            var extra = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extra.name = "Extra";
            extra.transform.SetParent(_root.transform);
            extra.GetComponent<Renderer>().sharedMaterials = new[] { mat };
            BodyRenderer.sharedMaterials = new[] { mat, mat };

            Assert.AreEqual(1, _check.Execute(_root).ToList().Count);
        }

        [Test]
        public void 非アクティブなオブジェクトも走査する()
        {
            SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.white);
            BodyRenderer.gameObject.SetActive(false);

            Assert.AreEqual(1, _check.Execute(_root).ToList().Count);
        }

        // ------------------------------------------------------------------
        // 輝度ゲート（実測較正の裏付け）
        // ------------------------------------------------------------------

        [Test]
        public void 輝度は実効発光色のRec709輝度になる()
        {
            var mat = SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.white, blend: 1f);
            var profile = EmissionShaderProfile.Find(mat);

            // 白 × 強度1 は L=1.0（カラースペースによらず白は白）
            Assert.AreEqual(1f, EmissionCheck.GetEffectiveLuminance(profile, mat), 0.001f);

            if (mat.HasProperty("_EmissionBlend"))
            {
                mat.SetFloat("_EmissionBlend", 0.5f);
                Assert.AreEqual(0.5f, EmissionCheck.GetEffectiveLuminance(profile, mat), 0.001f);
            }

            // 緑は Rec.709 で最も重みが大きい（0.7152）
            mat.SetColor("_EmissionColor", Color.green);
            if (mat.HasProperty("_EmissionBlend"))
                mat.SetFloat("_EmissionBlend", 1f);
            Assert.AreEqual(0.7152f, EmissionCheck.GetEffectiveLuminance(profile, mat), 0.001f);
        }

        [Test]
        public void 閾値付近の発光は境界どおりに判定する()
        {
            var mat = SetupLilToonMaterial(useEmission: 1f, emissionColor: Color.white, blend: 1f);
            if (!mat.HasProperty("_EmissionBlend"))
                Assert.Ignore("このシェーダーは _EmissionBlend を持たない（lilToon Lite 相当）");

            var profile = EmissionShaderProfile.Find(mat);

            // 閾値のわずかに下 → スキップ
            mat.SetFloat("_EmissionBlend", EmissionCheck.LuminanceThreshold * 0.9f);
            Assert.Less(EmissionCheck.GetEffectiveLuminance(profile, mat), EmissionCheck.LuminanceThreshold);
            Assert.IsEmpty(_check.Execute(_root).ToList());

            // 閾値のわずかに上 → 検出
            mat.SetFloat("_EmissionBlend", EmissionCheck.LuminanceThreshold * 1.1f);
            Assert.GreaterOrEqual(EmissionCheck.GetEffectiveLuminance(profile, mat), EmissionCheck.LuminanceThreshold);
            Assert.AreEqual(1, _check.Execute(_root).ToList().Count);
        }
    }
}
