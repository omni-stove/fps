using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    // smoke とテストが期待する行を同じ関数で作れるよう、文言はここにまとめる。
    public static class DeployLog
    {
        public const string Tag = "[Deploy]";

        public static string Prefix(string ownerId, string kindId)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)}";
        }

        public static string Preview(string ownerId, string kindId, DeployVerdict verdict)
        {
            return $"{Prefix(ownerId, kindId)} preview verdict={verdict}";
        }

        public static string Placed(string ownerId, string kindId, int serial, Vector3 position, int active, int maxActive)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} placed serial={1} pos=({2:0.00}, {3:0.00}, {4:0.00}) active={5}/{6}",
                Prefix(ownerId, kindId),
                serial,
                position.X,
                position.Y,
                position.Z,
                active,
                maxActive);
        }

        public static string Refused(string ownerId, string kindId, DeployVerdict verdict, int charges, int maxCharges)
        {
            return $"{Prefix(ownerId, kindId)} refused reason={verdict} charges={charges}/{maxCharges}";
        }

        public static string LimitReached(string ownerId, string kindId, int active, int maxActive)
        {
            return $"{Prefix(ownerId, kindId)} refused reason=Limit active={active}/{maxActive}";
        }

        public static string Cancelled(string ownerId, string kindId, int charges, int maxCharges)
        {
            return $"{Prefix(ownerId, kindId)} cancelled charges={charges}/{maxCharges}";
        }

        public static string Blocked(string ownerId, string kindId, int serial, int damage, int health, string shooterId)
        {
            return $"{Prefix(ownerId, kindId)} serial={serial} blocked dmg={damage} hp={health} shooter={Name(shooterId)}";
        }

        public static string Removed(string ownerId, string kindId, int serial, DeployRemoval reason)
        {
            return $"{Prefix(ownerId, kindId)} serial={serial} removed reason={reason}";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
