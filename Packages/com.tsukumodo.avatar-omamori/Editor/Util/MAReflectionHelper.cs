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
        private static FieldInfo s_mergeArmatureMergeTargetField;
        private static FieldInfo s_boneProxyBoneReferenceField;
        private static FieldInfo s_boneProxySubPathField;

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
        /// MergeArmature コンポーネントの mergeTarget が設定済みかどうかを判定する。
        /// AvatarObjectReference.Get(Component) でターゲットを解決し、null でないかを返す。
        /// </summary>
        public static bool IsMergeArmatureTargetSet(Component mergeArmature)
        {
            EnsureInitialized();
            if (mergeArmature == null || s_mergeArmatureMergeTargetField == null) return false;

            var avatarObjRef = s_mergeArmatureMergeTargetField.GetValue(mergeArmature);
            if (avatarObjRef == null) return false;

            return ResolveAvatarObjectReference(avatarObjRef, mergeArmature) != null;
        }

        /// <summary>
        /// BoneProxy コンポーネントのターゲットが設定済みかどうかを判定する。
        /// boneReference が HumanBodyBones.LastBone でない、または subPath が空でなければ設定済みとみなす。
        /// </summary>
        public static bool IsBoneProxyTargetSet(Component boneProxy)
        {
            EnsureInitialized();
            if (boneProxy == null) return false;

            // boneReference フィールドの確認
            if (s_boneProxyBoneReferenceField != null)
            {
                var boneRef = s_boneProxyBoneReferenceField.GetValue(boneProxy);
                if (boneRef != null && (int)boneRef != (int)HumanBodyBones.LastBone)
                    return true;
            }

            // subPath フィールドの確認
            if (s_boneProxySubPathField != null)
            {
                var subPath = s_boneProxySubPathField.GetValue(boneProxy) as string;
                if (!string.IsNullOrEmpty(subPath))
                    return true;
            }

            return false;
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

        /// <summary>
        /// AvatarObjectReference を解決して GameObject を返す共通メソッド。
        /// </summary>
        private static GameObject ResolveAvatarObjectReference(object avatarObjRef, Component context)
        {
            if (avatarObjRef == null) return null;

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

            if (s_targetObjectField != null)
            {
                try
                {
                    return s_targetObjectField.GetValue(avatarObjRef) as GameObject;
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

                // MergeArmature の mergeTarget フィールド（AvatarObjectReference 型）
                if (s_mergeArmatureType != null)
                {
                    s_mergeArmatureMergeTargetField = s_mergeArmatureType.GetField("mergeTarget",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // BoneProxy の boneReference / subPath フィールド
                if (s_boneProxyType != null)
                {
                    s_boneProxyBoneReferenceField = s_boneProxyType.GetField("boneReference",
                        BindingFlags.Public | BindingFlags.Instance);
                    s_boneProxySubPathField = s_boneProxyType.GetField("subPath",
                        BindingFlags.Public | BindingFlags.Instance);
                }

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
