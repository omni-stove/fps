using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary><see cref="ProgressionLedger"/> が 1 つの出来事を記録した結果。新しいプロフィールと、得た XP と、前後のレベルを持つ。</summary>
    public sealed class ProgressionAward
    {
        /// <exception cref="ArgumentNullException">profile が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">xp が負か、レベルが 1 未満か、level が previousLevel より低い。</exception>
        public ProgressionAward(PlayerProfile profile, int xp, int previousLevel, int level)
        {
            if (xp < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(xp), xp, "xp must not be negative");
            }

            if (previousLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(previousLevel), previousLevel, "previousLevel must be 1 or more");
            }

            if (level < previousLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must not be lower than previousLevel");
            }

            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Xp = xp;
            PreviousLevel = previousLevel;
            Level = level;
        }

        public PlayerProfile Profile { get; }

        /// <summary>この出来事で増えた XP。累計が int の上限で止まったときは、実際に増えた分。</summary>
        public int Xp { get; }

        public int PreviousLevel { get; }

        public int Level { get; }

        public bool LeveledUp => Level > PreviousLevel;

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "+{0} xp level {1}->{2} ({3})", Xp, PreviousLevel, Level, Profile);
        }
    }
}
