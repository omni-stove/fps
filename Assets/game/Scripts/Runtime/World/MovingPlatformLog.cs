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

        public static string CallZoneWithoutPlatform(string zone)
        {
            return $"{LogTag} 呼び出し口 '{zone}' に呼ぶ床 (MovingPlatform) が設定されていません。";
        }

        public static string CallZoneStopOutOfRange(string zone, string platform, int stop, int count)
        {
            return $"{LogTag} 呼び出し口 '{zone}' の stop={stop} は '{platform}' の点の数 ({count}) の外です。";
        }

        public static string Called(string platform, int stop, PlatformRouteKind kind, bool accepted)
        {
            return accepted
                ? $"{LogTag} '{platform}' called stop={stop}"
                : $"{LogTag} '{platform}' ignored call stop={stop} kind={kind}";
        }
    }
}
