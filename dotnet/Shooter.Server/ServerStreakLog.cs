using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 数え方と使ったことの行は Core の StreakLog と同じ書式にし、オフラインのログと同じ行で突き合わせられるようにする。
    // ここにはサーバーにだけある行 (断った理由・UAV の印・空爆の着弾) を置く。
    internal static class ServerStreakLog
    {
        public const string LogTag = StreakLog.LogTag;

        public static string Rule(Streaks streaks, StreakSpec spec)
        {
            return LogTag + " net rule=" + streaks
                + " uavSeconds=" + Text(spec.Uav.DurationSeconds) + " uavRange=" + Text(spec.Uav.Range)
                + " airstrikeShells=" + Text(spec.Airstrike.ShellCount) + " airstrikeRange=" + Text(spec.Airstrike.Range);
        }

        public static string Id(int playerId) => "net" + Text(playerId);

        public static string Refused(int playerId, string rewardId, string reason)
        {
            return LogTag + " id=" + Id(playerId) + " use refused reward=" + (string.IsNullOrEmpty(rewardId) ? "none" : rewardId) + " reason=" + reason;
        }

        public static string UavMarked(int playerId, Team team, int marked, int recipients, float durationSeconds)
        {
            return LogTag + " id=" + Id(playerId) + " uav team=" + team + " marked=" + Text(marked) + " recipients=" + Text(recipients)
                + " seconds=" + Text(durationSeconds);
        }

        public static string UavCleared(int marks)
        {
            return LogTag + " net uav cleared marks=" + Text(marks);
        }

        public static string AirstrikeCalled(int playerId, Vector3 center, float headingDegrees, int shells, float intervalSeconds, float lineLength)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} airstrike at=({2:0.0}, {3:0.0}, {4:0.0}) heading={5:0.0} shells={6} interval={7} line={8}",
                LogTag,
                Id(playerId),
                center.X,
                center.Y,
                center.Z,
                headingDegrees,
                shells,
                intervalSeconds,
                lineLength);
        }

        public static string AirstrikeShell(int playerId, int index, int count, int damaged, int killed)
        {
            return LogTag + " id=" + Id(playerId) + " airstrike shell=" + Text(index) + "/" + Text(count)
                + " damaged=" + Text(damaged) + " killed=" + Text(killed);
        }

        public static string AirstrikeCancelled(int shells)
        {
            return LogTag + " net airstrike cancelled shells=" + Text(shells);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
