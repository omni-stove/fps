using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 数はプロフィールにだけ持ち、ここは条件と照らすだけにする。数を 2 か所に持つと、保存と画面の数が食い違うため。
    public sealed class AchievementTracker
    {
        public AchievementTracker(AchievementSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public AchievementSpec Spec { get; }

        public static int CountOf(PlayerProfile profile, PlayerStat stat)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            switch (stat)
            {
                case PlayerStat.Kills:
                    return profile.Kills;
                case PlayerStat.Deaths:
                    return profile.Deaths;
                case PlayerStat.Assists:
                    return profile.Assists;
                case PlayerStat.Headshots:
                    return profile.Headshots;
                case PlayerStat.MeleeKills:
                    return profile.MeleeKills;
                case PlayerStat.MatchesPlayed:
                    return profile.Matches;
                case PlayerStat.MatchesWon:
                    return profile.Wins;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "stat must be a PlayerStat value");
            }
        }

        // 頭と近接は 1 回のキルで両方に当たりうるので、別々に数える。キルそのものは ProgressionLedger が数える。
        public PlayerProfile RecordKill(PlayerProfile profile, bool headshot, string weaponId)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var headshots = headshot ? Saturated(profile.Headshots, 1) : profile.Headshots;
            var meleeKills = Spec.IsMelee(weaponId) ? Saturated(profile.MeleeKills, 1) : profile.MeleeKills;
            if (headshots == profile.Headshots && meleeKills == profile.MeleeKills)
            {
                return profile;
            }

            return profile.WithStats(headshots, meleeKills, profile.PlaySeconds);
        }

        public static PlayerProfile RecordPlayTime(PlayerProfile profile, double seconds)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (!(seconds >= 0d) || double.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative and must be finite");
            }

            var whole = (long)Math.Floor(seconds);
            if (whole == 0)
            {
                return profile;
            }

            var total = (int)Math.Min(int.MaxValue, profile.PlaySeconds + whole);
            return profile.WithStats(profile.Headshots, profile.MeleeKills, total);
        }

        // 解けた順を定義の並びにそろえるのは、同時に解けたものの並びを保存のたびに変えないため。
        public PlayerProfile Unlock(PlayerProfile profile, List<AchievementDefinition> unlocked)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (unlocked == null)
            {
                throw new ArgumentNullException(nameof(unlocked));
            }

            var next = profile;
            foreach (var achievement in Spec.Achievements)
            {
                if (next.HasAchievement(achievement.Id) || CountOf(next, achievement.Stat) < achievement.Target)
                {
                    continue;
                }

                next = next.WithAchievement(achievement.Id);
                unlocked.Add(achievement);
            }

            return next;
        }

        // 定義から外れた解いた記録は保存に残したまま、ここでは今の定義の分だけを返す。
        public IReadOnlyList<AchievementProgress> Progress(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var progress = new AchievementProgress[Spec.Achievements.Count];
            for (var i = 0; i < progress.Length; i++)
            {
                var achievement = Spec.Achievements[i];
                progress[i] = new AchievementProgress(
                    achievement.Id, CountOf(profile, achievement.Stat), achievement.Target, profile.HasAchievement(achievement.Id));
            }

            return Array.AsReadOnly(progress);
        }

        // 長く遊び続けても数が負に回り込んでプロフィールが壊れないよう、int の上限で止める。
        private static int Saturated(int value, int add)
        {
            return (int)Math.Min(int.MaxValue, (long)value + add);
        }
    }
}
