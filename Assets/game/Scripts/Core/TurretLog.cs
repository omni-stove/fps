using System.Globalization;

namespace Fps.Core
{
    // 置いた・消えた・撃たれて止めたは DeployLog に出す。ここはタレットが自分で動いた (狙った・撃った・倒した・控えた) ことだけ。
    public static class TurretLog
    {
        public const string Tag = "[Turret]";

        public static string Prefix(string ownerId, string kindId, int serial)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)} serial={serial}";
        }

        public static string Armed(string ownerId, string kindId, int serial, string weaponId, int rounds, float seconds, FriendlyFire friendlyFire)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} armed weapon={1} rounds={2} seconds={3:0.0} friendlyFire={4}",
                Prefix(ownerId, kindId, serial),
                Name(weaponId),
                rounds,
                seconds,
                friendlyFire);
        }

        public static string Target(string ownerId, string kindId, int serial, string targetId)
        {
            return $"{Prefix(ownerId, kindId, serial)} target={Name(targetId)}";
        }

        public static string Fired(string ownerId, string kindId, int serial, string targetId, string hitId, int damage, int roundsLeft)
        {
            return $"{Prefix(ownerId, kindId, serial)} fire target={Name(targetId)} hit={Name(hitId)} dmg={damage} rounds={roundsLeft}";
        }

        public static string Killed(string ownerId, string kindId, int serial, string victimId)
        {
            return $"{Prefix(ownerId, kindId, serial)} killed id={Name(victimId)}";
        }

        public static string Held(string ownerId, string kindId, int serial, string allyId, FriendlyFire friendlyFire)
        {
            return $"{Prefix(ownerId, kindId, serial)} hold ally={Name(allyId)} friendlyFire={friendlyFire}";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
