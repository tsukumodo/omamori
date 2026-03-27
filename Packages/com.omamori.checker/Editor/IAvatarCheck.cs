using System.Collections.Generic;
using UnityEngine;

namespace AvatarOmamori.Editor
{
    public interface IAvatarCheck
    {
        string DisplayName { get; }
        bool IsAvailable();
        IEnumerable<CheckResult> Execute(GameObject avatarRoot);
    }
}
