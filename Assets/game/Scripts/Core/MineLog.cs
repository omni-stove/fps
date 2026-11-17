using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // 置いた・消えた・撃たれて止めたは DeployLog に、爆発の当たりは Explosion のログに出す。ここは地雷が有効になったことと、誰が踏んだかだけ。
    public static class MineLog
    {
        public const string Tag = "[Mine]";

        public static string Prefix(string ownerId, string kindId, int serial)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)} serial={serial}";
        }

        public static string Arming(string ownerId, string kindId, int serial, float armSeconds, float triggerRadius, int maxDamage, float damageRadius)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} arming seconds={1:0.0} trigger={2:0.0} dmg={3} radius={4:0.0}",
                Prefix(ownerId, kindId, serial),
                armSeconds,
                triggerRadius,
                maxDamage,
                damageRadius);
        }

        public static string Armed(string ownerId, string kindId, int serial)
        {
            return $"{Prefix(ownerId, kindId, serial)} armed";
        }

        public static string Triggered(string ownerId, string kindId, int serial, string intruderId, Vector3 at)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} triggered by={1} at=({2:0.00}, {3:0.00}, {4:0.00})",
                Prefix(ownerId, kindId, serial),
                Name(intruderId),
                at.X,
                at.Y,
                at.Z);
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
