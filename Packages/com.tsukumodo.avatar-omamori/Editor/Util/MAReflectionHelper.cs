using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace AvatarOmamori.Editor.Util
{
    /// <summary>
    /// Modular Avatar の型・フィールドを Reflection で動的に取得するヘルパー。
    /// MA が未インストールの場合は null を返し、チェックをスキップできるようにする。
    /// </summary>
    public static class MAReflectionHelper
    {
        private static bool s_initialized;
        private static Assembly s_maAssembly;
        private static Type s_menuItemType;
        private static Type s_menuInstallerType;
        private static Type s_objectToggleType;
        private static Type s_mergeArmatureType;
        private static Type s_boneProxyType;
        private static FieldInfo s_objectsField;
        private static Type s_toggledObjectType;
        private static FieldInfo s_toggledObjectObjectField;
        private static Type s_avatarObjectReferenceType;
        private static MethodInfo s_avatarObjectReferenceGetMethod;
        private static FieldInfo s_targetObjectField;

        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return s_maAssembly != null;
            }
        }

        public static Type MenuItemType
        {
            get { EnsureInitialized(); return s_menuItemType; }
        }

        public static Type MenuInstallerType
        {
            get { EnsureInitialized(); return s_menuInstallerType; }
        }

        public static Type ObjectToggleType
        {
            get { EnsureInitialized(); return s_objectToggleType; }
        }

        public static Type MergeArmatureType
        {
            get { EnsureInitialized(); return s_mergeArmatureType; }
        }

        public static Type BoneProxyType
        {
            get { EnsureInitialized(); return s_boneProxyType; }
        }

        /// <summary>
        /// ObjectToggle コンポーネントから m_objects リスト (IList) を取得する。
        /// </summary>
        public static IList GetToggleObjects(Component objectToggle)
        {
            EnsureInitialized();
            if (s_objectsField == null) return null;
            return s_objectsField.GetValue(objectToggle) as IList;
        }

        /// <summary>
        /// ToggledObject から参照先の GameObject を解決する。
        /// 優先順位: Get(Component) メソッド → targetObject フィールド
        /// </summary>
        public static GameObject ResolveToggledTarget(object toggledObject, Component context)
        {
            EnsureInitialized();
            if (toggledObject == null) return null;

            // ToggledObject の Object フィールド (AvatarObjectReference) を取得
            var avatarObjRef = s_toggledObjectObjectField?.GetValue(toggledObject);
            if (avatarObjRef == null) return null;

            // 方法1: Get(Component) メソッドで解決
            if (s_avatarObjectReferenceGetMethod != null)
            {
                try
                {
                    var resolved = s_avatarObjectReferenceGetMethod.Invoke(avatarObjRef, new object[] { context });
                    if (resolved is GameObject go) return go;
                    if (resolved is Transform t) return t.gameObject;
                }
                catch
                {
                    // フォールバックへ
                }
            }

            // 方法2: targetObject フィールドの直接読み取り
            if (s_targetObjectField != null)
            {
                try
                {
                    var target = s_targetObjectField.GetValue(avatarObjRef) as GameObject;
                    if (target != null) return target;
                }
                catch
                {
                    // 解決不能
                }
            }

            return null;
        }

        private static void EnsureInitialized()
        {
            if (s_initialized) return;
            s_initialized = true;

            try
            {
                s_maAssembly = FindAssembly("nadena.dev.modular-avatar.core");
                if (s_maAssembly == null) return;

                s_menuItemType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarMenuItem");
                s_menuInstallerType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller");
                s_objectToggleType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarObjectToggle");
                s_mergeArmatureType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarMergeArmature");
                s_boneProxyType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarBoneProxy");

                if (s_objectToggleType != null)
                {
                    s_objectsField = s_objectToggleType.GetField("m_objects",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                // ToggledObject 型の取得
                s_toggledObjectType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.ToggledObject");
                if (s_toggledObjectType != null)
                {
                    s_toggledObjectObjectField = s_toggledObjectType.GetField("Object",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // AvatarObjectReference 型
                s_avatarObjectReferenceType = s_maAssembly.GetType("nadena.dev.modular_avatar.core.AvatarObjectReference");
                if (s_avatarObjectReferenceType != null)
                {
                    s_avatarObjectReferenceGetMethod = s_avatarObjectReferenceType.GetMethod("Get",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(Component) }, null);

                    s_targetObjectField = s_avatarObjectReferenceType.GetField("targetObject",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AvatarOmamori] MAReflectionHelper initialization failed: {e}");
                s_maAssembly = null;
            }
        }

        private static Assembly FindAssembly(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == name)
                    return asm;
            }
            return null;
        }
    }
}
