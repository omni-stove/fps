using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // 置いた・消えたは DeployLog に、爆発の当たりは Explosion のログに出す。ここは何に貼ったかと、起爆したことだけ。
    public static class RemoteChargeLog
    {
        public const string Tag = "[RemoteCharge]";

        public static string Prefix(string ownerId, string kindId)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)}";
        }

        public static string Attached(string ownerId, string kindId, int serial, string surface)
        {
            return $"{Prefix(ownerId, kindId)} serial={serial} attached to={Name(surface)}";
        }

        public static string Detonating(string ownerId, string kindId, int count)
        {
            return $"{Prefix(ownerId, kindId)} detonating count={count}";
        }

        public static string Detonated(string ownerId, string kindId, int serial, Vector3 at)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} serial={1} detonated at=({2:0.00}, {3:0.00}, {4:0.00})",
                Prefix(ownerId, kindId),
                serial,
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
