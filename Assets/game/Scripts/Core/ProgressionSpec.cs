using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class ProgressionSpec
    {
        public const int DefaultKillXp = 100;
        public const int DefaultHeadshotXp = 50;
        public const int DefaultAssistXp = 50;
        public const int DefaultMatchXp = 250;
        public const int DefaultWinXp = 500;

        private static readonly int[] _defaultLevels = { 0, 500, 1500, 3000, 5000, 7500, 10500, 14000, 18000, 22500 };

        public static readonly ProgressionSpec Default = new ProgressionSpec(
            DefaultKillXp, DefaultHeadshotXp, DefaultAssistXp, DefaultMatchXp, DefaultWinXp, _defaultLevels, Array.Empty<ProgressionUnlock>());

        public ProgressionSpec(
            int killXp, int headshotXp, int assistXp, int matchXp, int winXp, IEnumerable<int> levels, IEnumerable<ProgressionUnlock> unlocks)
        {
            RequireNonNegative(killXp, nameof(killXp));
            RequireNonNegative(headshotXp, nameof(headshotXp));
            RequireNonNegative(assistXp, nameof(assistXp));
            RequireNonNegative(matchXp, nameof(matchXp));
            RequireNonNegative(winXp, nameof(winXp));
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (unlocks == null)
            {
                throw new ArgumentNullException(nameof(unlocks));
            }

            var levelList = new List<int>(levels);
            if (levelList.Count == 0)
            {
                throw new ArgumentException("levels must have at least level 1", nameof(levels));
            }

            if (levelList[0] != 0)
            {
                throw new ArgumentException(
                    $"levels must start at 0 xp for level 1, but start at {levelList[0].ToString(CultureInfo.InvariantCulture)}", nameof(levels));
            }

            for (var i = 1; i < levelList.Count; i++)
            {
                if (levelList[i] <= levelList[i - 1])
                {
                    throw new ArgumentException(
                        $"levels must rise: level {(i + 1).ToString(CultureInfo.InvariantCulture)} needs {levelList[i].ToString(CultureInfo.InvariantCulture)} xp, "
                        + $"not more than level {i.ToString(CultureInfo.InvariantCulture)} ({levelList[i - 1].ToString(CultureInfo.InvariantCulture)})",
                        nameof(levels));
                }
            }

            var unlockList = new List<ProgressionUnlock>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unlock in unlocks)
            {
                if (unlock == null)
                {
                    throw new ArgumentException("unlocks must not contain null", nameof(unlocks));
                }

                if (!ids.Add(unlock.Id))
                {
                    throw new ArgumentException($"duplicate unlock id '{unlock.Id}'", nameof(unlocks));
                }

                if (unlock.Level > levelList.Count)
                {
                    throw new ArgumentException(
                        $"unlock '{unlock.Id}' needs level {unlock.Level.ToString(CultureInfo.InvariantCulture)}, "
                        + $"but the levels end at {levelList.Count.ToString(CultureInfo.InvariantCulture)}",
                        nameof(unlocks));
                }

                unlockList.Add(unlock);
            }

            KillXp = killXp;
            HeadshotXp = headshotXp;
            AssistXp = assistXp;
            MatchXp = matchXp;
            WinXp = winXp;
            Levels = new ReadOnlyCollection<int>(levelList);
            Unlocks = new ReadOnlyCollection<ProgressionUnlock>(unlockList);
        }

        public int KillXp { get; }

        public int HeadshotXp { get; }

        public int AssistXp { get; }

        public int MatchXp { get; }

        public int WinXp { get; }

        public IReadOnlyList<int> Levels { get; }

        public int MaxLevel => Levels.Count;

        public IReadOnlyList<ProgressionUnlock> Unlocks { get; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "kill={0} headshot={1} assist={2} match={3} win={4} levels={5} maxXp={6} unlocks={7}",
                KillXp, HeadshotXp, AssistXp, MatchXp, WinXp, Levels.Count, Levels[Levels.Count - 1], Unlocks.Count);
        }

        private static void RequireNonNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative");
            }
        }
    }
}
