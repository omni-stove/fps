using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class StreakText
    {
        public const string KillsKey = "streak.kills";

        public const string NextKey = "streak.next";

        public const string ReadyKey = "streak.ready";

        public const string ReadyWithoutKeyKey = "streak.readyNoKey";

        public static LocalizedText Kills(StreakStatus status)
        {
            return new LocalizedText(KillsKey, status.Kills);
        }

        public static bool TryFindNext(StreakStatus status, out StreakSlot next)
        {
            next = default;
            var found = false;
            foreach (var slot in status.Slots)
            {
                if (slot.Ready || slot.KillsRequired <= status.Kills)
                {
                    continue;
                }

                if (!found || slot.KillsRequired < next.KillsRequired)
                {
                    next = slot;
                    found = true;
                }
            }

            return found;
        }

        public static LocalizedText Next(StreakStatus status, StreakSlot next)
        {
            return new LocalizedText(NextKey, next.KillsRequired - status.Kills, DisplayNames.Of(next));
        }

        public static LocalizedText Ready(StreakSlot slot, string keyLabel)
        {
            var name = DisplayNames.Of(slot);
            return string.IsNullOrWhiteSpace(keyLabel)
                ? new LocalizedText(ReadyWithoutKeyKey, name)
                : new LocalizedText(ReadyKey, keyLabel.Trim(), name);
        }

        public static List<LocalizedText> Lines(StreakStatus status, string keyLabel)
        {
            var lines = new List<LocalizedText> { Kills(status) };
            if (TryFindNext(status, out var next))
            {
                lines.Add(Next(status, next));
            }

            foreach (var slot in status.Slots)
            {
                if (slot.Ready)
                {
                    lines.Add(Ready(slot, keyLabel));
                }
            }

            return lines;
        }
    }
}
