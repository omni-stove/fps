using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class ProgressionAward
    {
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
