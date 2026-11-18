using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 数え方と使ったことの行は Core の StreakLog と同じ書式にし、ここにはオンラインにだけある行を置く。
    public static class ClientStreakLog
    {
        public static string Id(int playerId)
        {
            return "net" + playerId.ToString(CultureInfo.InvariantCulture);
        }

        public static string Requested(string id, string rewardId, bool sent)
        {
            return StreakLog.LogTag + " id=" + id + " request reward=" + rewardId + (sent ? string.Empty : " not-sent");
        }

        public static string NotReadyHere(string id, string rewardId)
        {
            return StreakLog.LogTag + " id=" + id + " server used reward=" + rewardId + " not ready here";
        }

        public static string Activated(int playerId, Team team, string rewardId, bool allied, float durationSeconds)
        {
            return StreakLog.LogTag + " net activated player=" + playerId.ToString(CultureInfo.InvariantCulture) + " team=" + team
                + " reward=" + rewardId + " side=" + (allied ? "ally" : "enemy")
                + " seconds=" + durationSeconds.ToString(CultureInfo.InvariantCulture);
        }
    }
}
