using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerInventoryLog
    {
        public static string Started(ItemCatalog catalog)
        {
            var rule = catalog.Rule;
            return ServerInventories.LogTag + " items=" + Text(catalog.Items.Count) + " starting=" + Text(catalog.Starting.Count)
                + " maxStacks=" + Text(rule.MaxStacks) + " maxWeight=" + Text(rule.MaxWeight)
                + " grid=" + Text(rule.GridWidth) + "x" + Text(rule.GridHeight) + " rotation=" + (rule.AllowRotation ? "on" : "off")
                + " carryLoad=" + (rule.CountWeightInCarryLoad ? "on" : "off");
        }

        public static string Give(int playerId, string itemId, int requested, int given)
        {
            return ServerInventories.LogTag + " give owner=" + Text(playerId) + " item=" + Id(itemId)
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

        public static string Move(int playerId, int handle, int x, int y, bool rotated, string result)
        {
            return ServerInventories.LogTag + " move owner=" + Text(playerId) + " handle=" + Text(handle) + " to=" + Text(x) + "," + Text(y)
                + (rotated ? " rotated" : string.Empty) + " result=" + result;
        }

        public static string Equipped(int playerId, string weaponId)
        {
            return ServerInventories.LogTag + " equip owner=" + Text(playerId) + " weapon=" + Id(weaponId);
        }

        public static string EquipRefused(int playerId, string weaponId, string reason)
        {
            return Equipped(playerId, weaponId) + " rejected=" + reason;
        }

        public static string Used(int playerId, string itemId, int healed)
        {
            return ServerInventories.LogTag + " use owner=" + Text(playerId) + " item=" + Id(itemId) + " health=+" + Text(healed);
        }

        public static string UseRefused(int playerId, int handle, string reason)
        {
            return ServerInventories.LogTag + " use owner=" + Text(playerId) + " handle=" + Text(handle) + " rejected=" + reason;
        }

        public static string Dropped(int playerId, string itemId, int count)
        {
            return ServerInventories.LogTag + " drop owner=" + Text(playerId) + " item=" + Id(itemId) + " count=" + Text(count) + " discarded";
        }

        public static string DropRefused(int playerId, int handle, int count, string reason)
        {
            return ServerInventories.LogTag + " drop owner=" + Text(playerId) + " handle=" + Text(handle) + " count=" + Text(count) + " rejected=" + reason;
        }

        public static string DropWeaponRefused(int playerId, string weaponId)
        {
            return ServerInventories.LogTag + " drop owner=" + Text(playerId) + " weapon=" + Id(weaponId) + " rejected=weapon";
        }

        public static string Attach(int playerId, int handle, string itemId, string weaponId, string replaced, string rejection)
        {
            return ServerInventories.LogTag + " attach owner=" + Text(playerId) + " handle=" + Text(handle) + " item=" + Id(itemId) + " weapon=" + Id(weaponId)
                + " replaced=" + Id(replaced) + Rejected(rejection);
        }

        public static string Detach(int playerId, string weaponId, string itemId, string rejection)
        {
            return ServerInventories.LogTag + " detach owner=" + Text(playerId) + " weapon=" + Id(weaponId) + " item=" + Id(itemId) + Rejected(rejection);
        }

        public static string WeaponOutside(int playerId, string weaponId)
        {
            return ServerInventories.LogTag + " weapon owner=" + Text(playerId) + " weapon=" + Id(weaponId) + " does not fit; held outside the inventory";
        }

        public static string Restock(int playerId, int ammo)
        {
            return ServerInventories.LogTag + " restock owner=" + Text(playerId) + " ammo=" + Text(ammo);
        }

        private static string Rejected(string rejection)
        {
            return string.IsNullOrEmpty(rejection) ? string.Empty : " rejected=" + rejection;
        }

        private static string Id(string id) => string.IsNullOrEmpty(id) ? "-" : id;

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
