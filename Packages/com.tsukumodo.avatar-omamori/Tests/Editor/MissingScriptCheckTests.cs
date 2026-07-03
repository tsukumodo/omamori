using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AvatarOmamori.Editor;
using AvatarOmamori.Editor.Checks;

namespace AvatarOmamori.Tests.Editor
{
    public class MissingScriptCheckTests
    {
        private const string PrefabPath = "Assets/__omamori_missing_script_fixture.prefab";

        private string _statsDir;
        private GameObject _instance;
        private readonly MissingScriptCheck _check = new MissingScriptCheck();

        [SetUp]
        public void SetUp()
        {
            // FixAction が利用統計を記録するため、保存先を一時フォルダへ退避する
            _statsDir = UsageStatsTestUtil.BeginOverride();
            FixHistoryStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            UsageStatsTestUtil.EndOverride(_statsDir);
            FixHistoryStore.Clear();
            if (_instance != null) Object.DestroyImmediate(_instance);
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        [Test]
        public void MissingScriptがなければ検出しない()
        {
            _instance = new GameObject("Avatar");

            Assert.IsEmpty(_check.Execute(_instance).ToList());
        }

        [Test]
        public void MissingScriptを検出し修正で削除できる()
        {
            _instance = CreateInstanceWithMissingScript();

            var results = _check.Execute(_instance).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Severity.Error, results[0].Severity);
            Assert.IsTrue(results[0].HasFix);

            results[0].FixAction();

            Assert.IsEmpty(_check.Execute(_instance).ToList(), "修正後も Missing Script が残っている");
            Assert.AreEqual(1, FixHistoryStore.Count, "修正履歴が記録されていない");
        }

        /// <summary>
        /// Missing Script はコードから直接は作れないため、DummyBehaviour 付きの Prefab を保存してから
        /// .prefab テキスト内のスクリプト GUID を存在しない GUID に書き換えて生成する。
        /// </summary>
        private static GameObject CreateInstanceWithMissingScript()
        {
            var go = new GameObject("MissingScriptFixture");
            go.AddComponent<DummyBehaviour>();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            var text = File.ReadAllText(PrefabPath);
            text = Regex.Replace(text, @"guid: [0-9a-f]{32}", "guid: deadbeefdeadbeefdeadbeefdeadbeef");
            File.WriteAllText(PrefabPath, text);
            AssetDatabase.ImportAsset(PrefabPath);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Prefab インスタンスのままだとコンポーネント削除が制限されるため、修正テスト用に unpack する
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }
    }
}
