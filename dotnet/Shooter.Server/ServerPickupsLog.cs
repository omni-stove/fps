using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerPickupsLog
    {
        // オフラインの拾い物と同じ札にして、同じ grep で両方を追えるようにする
        public const string LogTag = PickupLog.Tag;

        public static string Started(PickupDropSpec drops)
        {
            return LogTag + " net dropsWeapon=" + drops.DropsWeapon + " dropAmmo=" + Text(drops.ReserveAmmo)
                + " dropLifetime=" + Text(drops.LifetimeSeconds) + " dropReach=" + Text(drops.Reach);
        }

        public static string Placed(string map, int pickups, int crates)
        {
            return LogTag + " net placed pickups=" + Text(pickups) + " ammoCrates=" + Text(crates) + " map=" + map;
        }

        public static string UnknownWeapon(string source, string weaponId)
        {
            return LogTag + " net skipped " + source + " weapon=" + weaponId + " is not in the loadout or the weapon catalog";
        }

        public static string Spawned(int itemId, string source, PickupSpec spec, Vector3 position, bool touch)
        {
            return LogTag + " net spawned item=" + Text(itemId) + " source=" + source + " " + PickupLog.Describe(spec)
                + " at=" + Text(position) + " touch=" + touch;
        }

        public static string Crate(int itemId, string id, Vector3 position, float cooldownSeconds)
        {
            return LogTag + " net ammo crate item=" + Text(itemId) + " source=map:" + id + " at=" + Text(position) + " cooldown=" + Text(cooldownSeconds);
        }

        public static string Respawned(int itemId)
        {
            return LogTag + " net respawned item=" + Text(itemId);
        }

        public static string Expired(int itemId)
        {
            return LogTag + " net expired item=" + Text(itemId);
        }

        public static string Dropped(int itemId, int ownerId, PickupSpec spec, Vector3 position)
        {
            return LogTag + " net dropped item=" + Text(itemId) + " owner=" + Text(ownerId) + " " + PickupLog.Describe(spec) + " at=" + Text(position);
        }

        public static string NoDrops(int playerId, string reason)
        {
            return LogTag + " net no drop player=" + Text(playerId) + " reason=" + reason;
        }

        public static string Collected(int itemId, int playerId, PickupOutcome outcome)
        {
            var line = LogTag + " net collected item=" + Text(itemId) + " by=" + Text(playerId) + " kind=" + outcome.Kind;
            if (outcome.WeaponId != null)
            {
                line += " weapon=" + outcome.WeaponId;
            }

            if (outcome.DroppedWeaponId != null)
            {
                line += " swapped=" + outcome.DroppedWeaponId;
            }

            if (outcome.ReserveAmmo > 0)
            {
                line += " ammo=" + Text(outcome.ReserveAmmo);
            }

            if (outcome.Health > 0)
            {
                line += " health=" + Text(outcome.Health);
            }

            if (outcome.ItemId != null)
            {
                line += " item=" + outcome.ItemId + " count=" + Text(outcome.Items);
            }

            return line;
        }

        public static string Left(int itemId, int amount)
        {
            return LogTag + " net left item=" + Text(itemId) + " amount=" + Text(amount);
        }

        public static string Refilled(int itemId, int playerId, int added, float cooldownSeconds)
        {
            return LogTag + " net ammo crate item=" + Text(itemId) + " by=" + Text(playerId) + " added=" + Text(added) + " readyIn=" + Text(cooldownSeconds);
        }

        // rejection は中身の判定で断ったとき (refusal が Judged) だけ添える
        public static string Refused(int itemId, int playerId, ServerPickupRefusal refusal, PickupRejection rejection)
        {
            return LogTag + " net collect item=" + Text(itemId) + " by=" + Text(playerId) + " refused=" + refusal
                + (refusal == ServerPickupRefusal.Judged ? " rejection=" + rejection : string.Empty);
        }

        public static string Failed(int itemId, int playerId, string reason)
        {
            return LogTag + " net collect item=" + Text(itemId) + " by=" + Text(playerId) + " failed=" + reason;
        }

        public static string Unheld(int playerId, string weaponId)
        {
            return LogTag + " net weapon owner=" + Text(playerId) + " weapon=" + weaponId + " does not fit the inventory; held outside it";
        }

        public static string Attachment(int itemId, int playerId, string attachmentId, bool intoInventory)
        {
            return LogTag + " net attachment item=" + Text(itemId) + " by=" + Text(playerId) + " attachment=" + attachmentId
                + (intoInventory ? " -> inventory" : " -> dropped");
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Text(Vector3 value) => "(" + Text(value.X) + "," + Text(value.Y) + "," + Text(value.Z) + ")";
    }
}
