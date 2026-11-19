using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class StreakLog
    {
        public const string LogTag = "[Streak]";

        public static string Configured(string id, StreakSpec spec)
        {
            var rewards = new List<string>();
            if (spec != null)
            {
                foreach (var reward in spec.Rewards)
                {
                    rewards.Add(reward.Id + "@" + reward.KillsRequired.ToString(CultureInfo.InvariantCulture));
                }
            }

            return $"{LogTag} id={id} rewards={(rewards.Count == 0 ? "none" : string.Join(",", rewards))}";
        }

        public static string Killed(string id, StreakStatus status)
        {
            return $"{LogTag} id={id} kill {Counts(status)}";
        }

        public static string Earned(string id, string rewardId, StreakStatus status)
        {
            return $"{LogTag} id={id} earned reward={rewardId} {Counts(status)}";
        }

        public static string Died(string id, int lostKills, StreakStatus status)
        {
            return $"{LogTag} id={id} died lost={lostKills.ToString(CultureInfo.InvariantCulture)} {Counts(status)}";
        }

        public static string Used(string id, string rewardId, StreakStatus status)
        {
            return $"{LogTag} id={id} used reward={rewardId} {Counts(status)}";
        }

        public static string Selected(string id, string rewardId, StreakStatus status)
        {
            return $"{LogTag} id={id} selected reward={rewardId} {Counts(status)}";
        }

        public static string NothingToUse(string id)
        {
            return $"{LogTag} id={id} use refused nothing-ready";
        }

        public static string UseRefusedDead(string id)
        {
            return $"{LogTag} id={id} use refused dead";
        }

        public static string RewardUnavailable(string id, string rewardId, string missing)
        {
            return $"{LogTag} id={id} use refused reward={rewardId} missing={missing}";
        }

        public static string TargetingStarted(string id, string rewardId)
        {
            return $"{LogTag} id={id} targeting started reward={rewardId}";
        }

        public static string TargetingConfirmed(string id, StreakTargetingStatus status)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} targeting confirmed reward={2} at=({3:0.0}, {4:0.0}, {5:0.0}) heading={6:0.0}",
                LogTag,
                id,
                status.RewardId,
                status.Target.X,
                status.Target.Y,
                status.Target.Z,
                status.HeadingDegrees);
        }

        public static string TargetingCancelled(string id, string rewardId, string reason)
        {
            return $"{LogTag} id={id} targeting cancelled reward={rewardId} reason={reason}";
        }

        private static string Counts(StreakStatus status)
        {
            return "kills=" + status.Kills.ToString(CultureInfo.InvariantCulture) + " ready=" + status.ReadyCount.ToString(CultureInfo.InvariantCulture);
        }
    }
}
