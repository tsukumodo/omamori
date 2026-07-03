using UnityEngine;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// MissingScriptCheck のテストフィクスチャ生成用の空 MonoBehaviour。
    /// Prefab に保存した後、スクリプト GUID を書き換えて Missing Script 化する。
    /// GameObject に AddComponent するため、Editor アセンブリではなくランタイム
    /// アセンブリ（このフォルダ）に置く必要がある。
    /// </summary>
    public sealed class DummyBehaviour : MonoBehaviour
    {
    }
}
