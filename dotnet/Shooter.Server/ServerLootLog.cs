using System.Globalization;
using System.Numerics;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal static class ServerLootLog
    {
        public const string LogTag = "[Loot]";

        public static string Started(int seed, int tables, float dropReach)
        {
            return LogTag + " seed=" + Text(seed) + " tables=" + Text(tables) + " dropReach=" + Text(dropReach);
        }

        public static string Placed(string mapName, int boxes)
        {
            return LogTag + " placed boxes=" + Text(boxes) + " map=" + mapName;
        }

        public static string NoPoints(string mapName)
        {
            return LogTag + " placed boxes=0 map=" + mapName + " (no points file)";
        }

        public static string Box(int itemId, ObjectiveItemKind kind, string source, Vector3 position, int weapons, int items)
        {
            return LogTag + " box item=" + Text(itemId) + " kind=" + kind + " from=" + source + " at=" + Text(position)
                + " weapons=" + Text(weapons) + " items=" + Text(items);
        }

        public static string PlayerSource(int playerId)
        {
            return "player:" + Text(playerId);
        }

        public static string UnknownTable(string source, string tableId)
        {
            return LogTag + " table=" + tableId + " of " + source + " is not in loot.json; the box opens empty";
        }

        public static string NotAWeapon(string itemId, string weaponId)
        {
            return LogTag + " item=" + itemId + " weapon=" + weaponId + " is not in the loadout or the weapon catalog; left out of the box";
        }

        public static string UnknownItem(string itemId)
        {
            return LogTag + " item=" + itemId + " is not in inventory.json; left out of the box";
        }

        public static string NoDeathBox(int playerId)
        {
            return LogTag + " death-box owner=" + Text(playerId) + " skipped: the player has no body";
        }

        public static string Opened(int itemId, ObjectiveItemKind kind, int playerId, int weaponsGiven, int weapons, int itemsGiven, int items)
        {
            return LogTag + " open item=" + Text(itemId) + " kind=" + kind + " by=" + Text(playerId)
                + " weapons=" + Text(weaponsGiven) + "/" + Text(weapons) + " items=" + Text(itemsGiven) + "/" + Text(items)
                + (weaponsGiven < weapons || itemsGiven < items ? " left=lost" : string.Empty);
        }

        public static string Refused(int itemId, int playerId, InteractRefusal refusal)
        {
            return LogTag + " open item=" + Text(itemId) + " by=" + Text(playerId) + " refused=" + refusal;
        }

        public static string Unheld(int playerId, string weaponId)
        {
            return LogTag + " weapon owner=" + Text(playerId) + " weapon=" + weaponId + " does not fit the inventory; held outside it";
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(Vector3 value) => Text(value.X) + "," + Text(value.Y) + "," + Text(value.Z);
    }
}
