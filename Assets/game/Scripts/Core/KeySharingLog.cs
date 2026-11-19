using System.Collections.Generic;

namespace Shooter.Core
{
    public static class KeySharingLog
    {
        public const string LogTag = "[Input]";

        public static string Yielded(string action, string controlPath, IReadOnlyList<string> preferred)
        {
            return $"{LogTag} {action} yields {controlPath} to {string.Join(", ", preferred)}";
        }
    }
}
