using UnityEngine;

namespace AvatarOmamori.Editor.Util
{
    /// <summary>
    /// GameObject のヒエラルキーパスを取得するユーティリティ。
    /// </summary>
    public static class HierarchyPathUtil
    {
        /// <summary>
        /// 指定した GameObject のルートからのフルパスを "/" 区切りで返す。
        /// </summary>
        /// <param name="obj">パスを取得する対象の GameObject。</param>
        /// <returns>"Root/Child/Target" 形式のヒエラルキーパス。</returns>
        public static string GetHierarchyPath(GameObject obj)
        {
            var path = obj.name;
            var current = obj.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
