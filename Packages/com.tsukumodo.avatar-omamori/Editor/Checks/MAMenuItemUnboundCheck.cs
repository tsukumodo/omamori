using System.Collections.Generic;
using AvatarOmamori.Editor.Util;
using UnityEngine;

namespace AvatarOmamori.Editor.Checks
{
    /// <summary>
    /// MA MenuItem が存在するのに祖先に MenuInstaller がない場合にエラーを報告する。
    /// メニューに接続されていない MenuItem はビルド後に動作しない。
    /// </summary>
    public sealed class MAMenuItemUnboundCheck : IAvatarCheck
    {
        /// <inheritdoc/>
        public string DisplayName => "MA MenuItem 未接続チェック";

        /// <inheritdoc/>
        public bool IsAvailable() => MAReflectionHelper.IsAvailable
                                     && MAReflectionHelper.MenuItemType != null
                                     && MAReflectionHelper.MenuInstallerType != null;

        /// <inheritdoc/>
        public IEnumerable<CheckResult> Execute(GameObject avatarRoot)
        {
            var menuItemType = MAReflectionHelper.MenuItemType;
            var menuInstallerType = MAReflectionHelper.MenuInstallerType;

            var menuItems = avatarRoot.GetComponentsInChildren(menuItemType, true);

            foreach (var item in menuItems)
            {
                if (!HasAncestorInstaller(item.transform, menuInstallerType))
                {
                    yield return new CheckResult(
                        Severity.Error,
                        $"MA MenuItem \"{item.gameObject.name}\" の祖先に Menu Installer がありません。アップロード後、Expressionsメニューにトグルが表示されず操作できなくなります。",
                        item
                    );
                }
            }
        }

        private static bool HasAncestorInstaller(Transform current, System.Type installerType)
        {
            var t = current;
            while (t != null)
            {
                if (t.GetComponent(installerType) != null)
                    return true;
                t = t.parent;
            }
            return false;
        }
    }
}
