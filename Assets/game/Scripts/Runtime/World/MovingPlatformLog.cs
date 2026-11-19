using Shooter.Core;

namespace Shooter.Runtime
{
    public static class MovingPlatformLog
    {
        public const string LogTag = "[MovingPlatform]";

        public static string InvalidSettings(string platform, string reason)
        {
            return $"{LogTag} '{platform}' の設定値が不正です: {reason}";
        }

        public static string BuiltDefaultShape(string platform)
        {
            return $"{LogTag} '{platform}' に Collider が無いので、既定の板を作りました。";
        }

        public static string Reversed(string platform, int from, int to)
        {
            return $"{LogTag} '{platform}' 乗っている体が挟まれるので引き返します stop={from} -> stop={to}";
        }

        public static string Called(string platform, int stop, PlatformRouteKind kind, bool accepted)
        {
            return accepted
                ? $"{LogTag} '{platform}' called stop={stop}"
                : $"{LogTag} '{platform}' ignored call stop={stop} kind={kind}";
        }
    }
}
