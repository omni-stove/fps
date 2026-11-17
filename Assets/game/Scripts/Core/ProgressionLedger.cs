using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// <see cref="ProgressionSpec"/> に従って XP と戦績を数え、レベルとアンロックを答える。状態は持たず、
    /// 記録するたびに新しい <see cref="PlayerProfile"/> を <see cref="ProgressionAward"/> に入れて返す。
    /// </summary>
    public sealed class ProgressionLedger
    {
        private readonly Dictionary<string, int> _unlockLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public ProgressionLedger(ProgressionSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            foreach (var unlock in spec.Unlocks)
            {
                _unlockLevels.Add(unlock.Id, unlock.Level);
            }
        }

        public ProgressionSpec Spec { get; }

        /// <summary>1 人倒したことを記録する。倒した数を 1 増やし、<see cref="ProgressionSpec.KillXp"/> (頭なら <see cref="ProgressionSpec.HeadshotXp"/> も) を足す。</summary>
        /// <exception cref="ArgumentNullException">profile が null。</exception>
        public ProgressionAward RecordKill(PlayerProfile profile, bool headshot)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var gain = (long)Spec.KillXp + (headshot ? Spec.HeadshotXp : 0);
            return Award(profile, gain, Saturated(profile.Kills, 1), profile.Deaths, profile.Matches, profile.Wins);
        }

        /// <summary>
        /// 試合を終えたことを記録する。試合数を 1 増やし (勝ちなら勝った数も)、倒れた数に <see cref="MatchResult.Deaths"/> を足し、
        /// <see cref="ProgressionSpec.MatchXp"/> (勝ちなら <see cref="ProgressionSpec.WinXp"/> も) を足す。
        /// 倒した数は <see cref="RecordKill"/> で 1 人ずつ数えるので、<see cref="MatchResult.Kills"/> は足さない。
        /// </summary>
        /// <exception cref="ArgumentNullException">profile が null。</exception>
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
            return Award(profile, gain, profile.Kills, Saturated(profile.Deaths, result.Deaths), matches, wins);
        }

        /// <summary>累計の XP が xp のときのレベル。1 から <see cref="ProgressionSpec.MaxLevel"/> まで。</summary>
        /// <exception cref="ArgumentOutOfRangeException">xp が負。</exception>
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

        /// <exception cref="ArgumentNullException">profile が null。</exception>
        public int LevelOf(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return LevelOf(profile.Xp);
        }

        /// <summary>id が使えるようになるレベル。アンロックの表に無い Id は 1 (最初から使える)。</summary>
        /// <exception cref="ArgumentException">id が null か空。</exception>
        public int UnlockLevelOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            return _unlockLevels.TryGetValue(id, out var level) ? level : 1;
        }

        /// <summary>レベルが level のとき id が使えるか。アンロックの表に無い Id は常に使える。</summary>
        /// <exception cref="ArgumentException">id が null か空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">level が 1 未満。</exception>
        public bool IsUnlocked(string id, int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be 1 or more");
            }

            return UnlockLevelOf(id) <= level;
        }

        /// <exception cref="ArgumentNullException">profile が null。</exception>
        /// <exception cref="ArgumentException">id が null か空。</exception>
        public bool IsUnlocked(string id, PlayerProfile profile)
        {
            return IsUnlocked(id, LevelOf(profile));
        }

        private ProgressionAward Award(PlayerProfile profile, long gain, int kills, int deaths, int matches, int wins)
        {
            var xp = (int)Math.Min(int.MaxValue, profile.Xp + gain);
            var next = new PlayerProfile(xp, kills, deaths, matches, wins);
            return new ProgressionAward(next, xp - profile.Xp, LevelOf(profile.Xp), LevelOf(xp));
        }

        // 長く遊び続けても数が負に回り込んでプロフィールが壊れないよう、int の上限で止める。
        private static int Saturated(int value, int add)
        {
            return (int)Math.Min(int.MaxValue, (long)value + add);
        }
    }
}
