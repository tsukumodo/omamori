using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;
using AvatarOmamori.Editor.Performance;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 「全チェックが直し方（Hint）まで案内する」を、新しいチェックが増えたときも自動で守らせるためのテスト。
    ///
    /// <para>
    /// チェックを実際に発火させるフィクスチャを全種類作るのは現実的ではないため、2段構えにしている:
    /// </para>
    /// <list type="number">
    ///   <item>チェック一覧（<see cref="CheckRunner.Checks"/>）の型名集合を固定する。
    ///   新しいチェックを足すと必ずこのテストが落ち、テスト2側の扱い（フィクスチャを足す／
    ///   除外理由をコメントする）を決めることになる。黙って抜けない。</item>
    ///   <item>フィクスチャで実際に発火できるチェックについて、<c>IsDetail == false</c> の結果すべてが
    ///   <see cref="CheckResult.Hint"/> を非 null かつ非空で持つことを検証する。</item>
    /// </list>
    /// </summary>
    public class CheckHintCoverageTests
    {
        // ------------------------------------------------------------------
        // テスト1: チェック一覧の固定
        // ------------------------------------------------------------------

        [Test]
        public void CheckRunnerのCheck一覧は現在の11種類と完全一致する()
        {
            // Checks はアセンブリ内の IAvatarCheck 実装をリフレクション列挙するだけで
            // IsAvailable() は見ないため、MA 未インストール環境でも11件全てが列挙される。
            // ここは CheckRunnerDiscoveryTests の「含まれているか」ではなく「過不足がないか」を見る。
            // 新しいチェックが増える／減ると必ずこのテストが落ち、テスト2側の扱いを検討する契機になる。
            var names = CheckRunner.Checks.Select(c => c.GetType().Name).ToList();

            var expected = new[]
            {
                nameof(AnimatorLayerWeightCheck),
                nameof(DescriptorDuplicateCheck),
                nameof(EmissionCheck),
                nameof(EmptyParameterNameCheck),
                nameof(ExpressionParameterBitLimitCheck),
                nameof(MAMenuItemUnboundCheck),
                nameof(MAObjectToggleCheck),
                nameof(MAUnsetupAccessoryCheck),
                nameof(MissingScriptCheck),
                nameof(MissingShaderCheck),
                nameof(UnsupportedComponentCheck),
            };

            CollectionAssert.AreEquivalent(expected, names);
        }

        // ------------------------------------------------------------------
        // テスト2: 発火できるチェックのヒント網羅
        //
        // MA 依存の3チェック（MAMenuItemUnboundCheck / MAObjectToggleCheck /
        // MAUnsetupAccessoryCheck）は Modular Avatar のコンポーネント型を
        // MAReflectionHelper 経由のリフレクションで取得しており、MA が未インストールの
        // このリポジトリの CI 環境では IsAvailable() が false になり型も取得できない。
        // 既存テストにもこの3チェック用のフィクスチャは無く（Tests/Editor/ 確認済み）、
        // 新規に組むには MA パッケージのインストールが要る。ここでは含めず、
        // テスト1の一覧固定で「除外扱いのままでよいか」を新チェック追加時に再検討させる。
        // ------------------------------------------------------------------

        private static void AssertAllNonDetailHaveHint(IEnumerable<CheckResult> results, string checkName)
        {
            var list = results.ToList();
            Assert.IsNotEmpty(list, $"{checkName} のフィクスチャが発火しなかった（テストの前提が崩れている）");

            foreach (var r in list.Where(r => !r.IsDetail))
            {
                Assert.IsFalse(
                    string.IsNullOrEmpty(r.Hint),
                    $"{checkName} の結果 \"{r.Message}\" に Hint が設定されていない");
            }
        }

        [Test]
        public void MissingScriptCheck_発火した結果はHintを持つ()
        {
            const string prefabPath = "Assets/__omamori_hint_coverage_missing_script.prefab";
            GameObject instance = null;
            try
            {
                // Missing Script はコードから直接は作れないため、DummyBehaviour 付きの Prefab を保存してから
                // .prefab テキスト内のスクリプト GUID を存在しない GUID に書き換えて生成する
                // （MissingScriptCheckTests.CreateInstanceWithMissingScript と同じ手法）。
                var go = new GameObject("MissingScriptFixture");
                go.AddComponent<DummyBehaviour>();
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);

                var text = File.ReadAllText(prefabPath);
                text = Regex.Replace(text, @"guid: [0-9a-f]{32}", "guid: deadbeefdeadbeefdeadbeefdeadbeef");
                File.WriteAllText(prefabPath, text);
                AssetDatabase.ImportAsset(prefabPath);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                var check = new MissingScriptCheck();
                AssertAllNonDetailHaveHint(check.Execute(instance), nameof(MissingScriptCheck));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        public void MissingShaderCheck_nullマテリアルスロットはHintを持つ()
        {
            var root = new GameObject("Avatar");
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform);
                body.GetComponent<Renderer>().sharedMaterials = new Material[] { null };

                var check = new MissingShaderCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(MissingShaderCheck));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingShaderCheck_シェーダー未検出はHintを持つ()
        {
            var root = new GameObject("Avatar");
            Material mat = null;
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform);
                mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
                body.GetComponent<Renderer>().sharedMaterials = new[] { mat };

                var check = new MissingShaderCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(MissingShaderCheck));
            }
            finally
            {
                if (mat != null) Object.DestroyImmediate(mat);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AnimatorLayerWeightCheck_発火した結果はHintを持つ()
        {
            var root = new GameObject("Avatar");
            AnimatorController controller = null;
            try
            {
                controller = new AnimatorController();
                controller.AddLayer("Layer0");
                controller.AddLayer("Layer1");
                var layers = controller.layers;
                layers[0].defaultWeight = 1f;
                layers[1].defaultWeight = 0f; // ベースレイヤー以外を Weight=0 にして発火させる
                controller.layers = layers;

                var descriptor = root.AddComponent<VRCAvatarDescriptor>();
                descriptor.baseAnimationLayers = new[]
                {
                    new VRCAvatarDescriptor.CustomAnimLayer
                    {
                        type = VRCAvatarDescriptor.AnimLayerType.FX,
                        isDefault = false,
                        animatorController = controller,
                    },
                };

                var check = new AnimatorLayerWeightCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(AnimatorLayerWeightCheck));
            }
            finally
            {
                if (controller != null) Object.DestroyImmediate(controller);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DescriptorDuplicateCheck_サマリー行はHintを持つ()
        {
            var root = new GameObject("Avatar");
            try
            {
                root.AddComponent<VRCAvatarDescriptor>();
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform);
                child.AddComponent<VRCAvatarDescriptor>();

                var check = new DescriptorDuplicateCheck();
                // 内訳行（重複箇所）は isDetail: true で Hint を持たない仕様のため、
                // AssertAllNonDetailHaveHint 側で自動的に対象外になる
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(DescriptorDuplicateCheck));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EmissionCheck_発火した結果はHintを持つ()
        {
            var root = new GameObject("Avatar");
            Material mat = null;
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform);

                // 組み込みの Standard シェーダーで発光を有効にする（lilToon 相当のスタブ生成は
                // EmissionCheckTests に既にあり、ここでは重複を避けて Standard のみで検証する）
                mat = new Material(Shader.Find("Standard"));
                mat.name = "TestStandard";
                mat.SetColor("_EmissionColor", Color.white);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                body.GetComponent<Renderer>().sharedMaterials = new[] { mat };

                var check = new EmissionCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(EmissionCheck));
            }
            finally
            {
                if (mat != null) Object.DestroyImmediate(mat);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EmptyParameterNameCheck_Menuのみ設定時はHintを持つ()
        {
            var root = new GameObject("Avatar");
            VRCExpressionsMenu menu = null;
            try
            {
                var descriptor = root.AddComponent<VRCAvatarDescriptor>();
                menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                descriptor.expressionsMenu = menu;
                descriptor.expressionParameters = null;

                var check = new EmptyParameterNameCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(EmptyParameterNameCheck));
            }
            finally
            {
                if (menu != null) Object.DestroyImmediate(menu);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EmptyParameterNameCheck_エントリ名が空のときはHintを持つ()
        {
            var root = new GameObject("Avatar");
            VRCExpressionParameters parameters = null;
            try
            {
                var descriptor = root.AddComponent<VRCAvatarDescriptor>();
                parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                parameters.parameters = new[]
                {
                    new VRCExpressionParameters.Parameter
                    {
                        name = "",
                        valueType = VRCExpressionParameters.ValueType.Bool,
                        networkSynced = true,
                    },
                };
                descriptor.expressionParameters = parameters;

                var check = new EmptyParameterNameCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(EmptyParameterNameCheck));
            }
            finally
            {
                if (parameters != null) Object.DestroyImmediate(parameters);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExpressionParameterBitLimitCheck_発火した結果はHintを持つ()
        {
            var root = new GameObject("Avatar");
            VRCExpressionParameters parameters = null;
            try
            {
                var descriptor = root.AddComponent<VRCAvatarDescriptor>();
                parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                // Float(8bit) × 33 = 264bit で上限256bitを超過させる
                parameters.parameters = Enumerable.Range(0, 33)
                    .Select(i => new VRCExpressionParameters.Parameter
                    {
                        name = $"p{i}",
                        valueType = VRCExpressionParameters.ValueType.Float,
                        networkSynced = true,
                    })
                    .ToArray();
                descriptor.expressionParameters = parameters;

                var check = new ExpressionParameterBitLimitCheck();
                AssertAllNonDetailHaveHint(check.Execute(root), nameof(ExpressionParameterBitLimitCheck));
            }
            finally
            {
                if (parameters != null) Object.DestroyImmediate(parameters);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnsupportedComponentCheck_サマリー行はHintを持つ()
        {
            var root = new GameObject("Avatar");
            try
            {
                var child = new GameObject("Collider");
                child.transform.SetParent(root.transform);
                var collider = child.AddComponent<MeshCollider>();

                var scan = new List<QuestIncompatibility>
                {
                    new QuestIncompatibility(
                        "VRChat が対応していないコンポーネント",
                        1,
                        "（テスト）",
                        new List<Object> { collider.gameObject },
                        IncompatibilityScope.AllPlatforms,
                        components: new Component[] { collider })
                };

                var results = UnsupportedComponentCheck.BuildResults(scan);
                AssertAllNonDetailHaveHint(results, nameof(UnsupportedComponentCheck));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
