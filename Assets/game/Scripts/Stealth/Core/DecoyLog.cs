using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // 置いた・消えた・撃たれて止めたは DeployLog に出す。ここは囮が呼んだことと、敵が囮に気を引かれた・見失ったことだけ。
    public static class DecoyLog
    {
        public const string Tag = "[Decoy]";

        public static string Prefix(string ownerId, string kindId, int serial)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)} serial={serial}";
        }

        public static string Armed(string ownerId, string kindId, int serial, float seconds, float interval, float radius, Stealth stealth)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} armed seconds={1:0.0} interval={2:0.0} radius={3:0.0} stealth={4}",
                Prefix(ownerId, kindId, serial),
                seconds,
                interval,
                radius,
                stealth);
        }

        public static string Called(string ownerId, string kindId, int serial, Vector3 at)
        {
            return $"{Prefix(ownerId, kindId, serial)} call at={Point(at)}";
        }

        public static string EnemyPrefix(string enemyId)
        {
            return $"{Tag} enemy={Name(enemyId)}";
        }

        public static string Seen(string enemyId, string ownerId, string kindId, int serial)
        {
            return $"{EnemyPrefix(enemyId)} sees owner={Name(ownerId)} kind={Name(kindId)} serial={serial}";
        }

        public static string Heard(string enemyId, string ownerId, string kindId, int serial, Vector3 at)
        {
            return $"{EnemyPrefix(enemyId)} hears owner={Name(ownerId)} kind={Name(kindId)} serial={serial} at={Point(at)}";
        }

        public static string Lost(string enemyId, string ownerId, string kindId, int serial, Vector3 last, bool gone)
        {
            return $"{EnemyPrefix(enemyId)} lost owner={Name(ownerId)} kind={Name(kindId)} serial={serial} last={Point(last)} gone={gone}";
        }

        private static string Point(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", value.X, value.Y, value.Z);
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
