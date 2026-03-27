using UnityEngine;

namespace AvatarOmamori.Editor
{
    public enum Severity
    {
        Error,
        Warning,
        Info
    }

    public sealed class CheckResult
    {
        public Severity Severity { get; }
        public string Message { get; }
        public Object TargetObject { get; }

        public CheckResult(Severity severity, string message, Object targetObject = null)
        {
            Severity = severity;
            Message = message;
            TargetObject = targetObject;
        }
    }
}
