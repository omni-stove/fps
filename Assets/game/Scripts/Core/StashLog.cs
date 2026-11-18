using System.Globalization;

namespace Shooter.Core
{
    public static class StashLog
    {
        public const string Tag = "[Stash]";

        public static string BrokenSave(string key, string backupKey, string problem)
        {
            return Tag + " saved stash '" + key + "': " + problem + "; kept the text in '" + backupKey + "' and started from the starting items";
        }

        public static string OverCapacity(string key, int stacks, int capacity)
        {
            return Tag + " saved stash '" + key + "' has " + stacks.ToString(CultureInfo.InvariantCulture)
                + " stacks, more than the capacity " + capacity.ToString(CultureInfo.InvariantCulture) + "; keeping them all";
        }
    }
}
