using System.Globalization;

namespace Shooter.Server
{
    internal static class ServerInventoryLog
    {
        public static string Give(int playerId, string itemId, int requested, int given)
        {
            return ServerInventories.LogTag + " give owner=" + Text(playerId) + " item=" + (string.IsNullOrEmpty(itemId) ? "-" : itemId)
                + " count=" + Text(given) + "/" + Text(requested) + (given < requested ? " partial" : string.Empty);
        }

        public static string Take(int playerId, int stacks, int items)
        {
            return ServerInventories.LogTag + " take owner=" + Text(playerId) + " stacks=" + Text(stacks) + " items=" + Text(items);
        }

        public static string Clear(int playerId, int items)
        {
            return ServerInventories.LogTag + " clear owner=" + Text(playerId) + " items=" + Text(items);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
