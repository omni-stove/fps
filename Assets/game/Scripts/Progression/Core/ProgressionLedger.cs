using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ProgressionLedger
    {
        private readonly Dictionary<string, int> _unlockLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        public ProgressionLedger(ProgressionSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            foreach (var unlock in spec.Unlocks)
            {
                _unlockLevels.Add(unlock.Id, unlock.Level);
            }
        }

        public ProgressionSpec Spec { get; }

        public ProgressionAward RecordKill(PlayerProfile profile, bool headshot)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var gain = (long)Spec.KillXp + (headshot ? Spec.HeadshotXp : 0);
            return Award(profile, gain, Saturated(profile.Kills, 1), profile.Assists, profile.Deaths, profile.Matches, profile.Wins);
        }

        public ProgressionAward RecordAssist(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return Award(profile, Spec.AssistXp, profile.Kills, Saturated(profile.Assists, 1), profile.Deaths, profile.Matches, profile.Wins);
        }

        // MatchResult.Kills を足さないのは、倒した数は RecordKill で 1 人ずつ数えてあるため。
        public ProgressionAward RecordMatch(PlayerProfile profile, MatchResult result)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var won = result.Outcome == MatchOutcome.Won;
            var gain = (long)Spec.MatchXp + (won ? Spec.WinXp : 0);
            var matches = Saturated(profile.Matches, 1);
            var wins = won ? Saturated(profile.Wins, 1) : profile.Wins;
            return Award(profile, gain, profile.Kills, profile.Assists, Saturated(profile.Deaths, result.Deaths), matches, wins);
        }

        public int LevelOf(int xp)
        {
            if (xp < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(xp), xp, "xp must not be negative");
            }

            var levels = Spec.Levels;
            var level = 1;
            while (level < levels.Count && levels[level] <= xp)
            {
                level++;
            }

            return level;
        }

        public int LevelOf(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return LevelOf(profile.Xp);
        }

        public int UnlockLevelOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            return _unlockLevels.TryGetValue(id, out var level) ? level : 1;
        }

        public bool IsUnlocked(string id, int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be 1 or more");
            }

            return UnlockLevelOf(id) <= level;
        }

        public bool IsUnlocked(string id, PlayerProfile profile)
        {
            return IsUnlocked(id, LevelOf(profile));
        }

        private ProgressionAward Award(PlayerProfile profile, long gain, int kills, int assists, int deaths, int matches, int wins)
        {
            var xp = (int)Math.Min(int.MaxValue, profile.Xp + gain);
            var next = profile.WithRecord(xp, kills, deaths, matches, wins, assists);
            return new ProgressionAward(next, xp - profile.Xp, LevelOf(profile.Xp), LevelOf(xp));
        }

        // 長く遊び続けても数が負に回り込んでプロフィールが壊れないよう、int の上限で止める。
        private static int Saturated(int value, int add)
        {
            return (int)Math.Min(int.MaxValue, (long)value + add);
        }
    }
}
