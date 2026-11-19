using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // 置いた・消えた・撃たれて止めたは DeployLog に出す。ここは飛び始めたこと・敵を見つけたこと・操縦の始まりと終わりと、戻したことだけ。
    public static class DroneLog
    {
        public const string Tag = "[Drone]";

        public static string Prefix(string ownerId, string kindId, int serial)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)} serial={serial}";
        }

        public static string Armed(string ownerId, string kindId, int serial, DroneSpec spec)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} armed speed={1:0.0} range={2:0.0} battery={3:0.0} radius={4:0.0} interval={5:0.00} reveal={6:0.0}",
                Prefix(ownerId, kindId, serial),
                spec.Speed,
                spec.ControlRange,
                spec.BatterySeconds,
                spec.SpotRadius,
                spec.SpotIntervalSeconds,
                spec.RevealSeconds);
        }

        public static string Spotted(string ownerId, string kindId, int serial, string targetId, Vector3 at, bool marked)
        {
            return $"{Prefix(ownerId, kindId, serial)} spotted target={Name(targetId)} at={Point(at)} marked={(marked ? "yes" : "no")}";
        }

        public static string PilotStarted(string ownerId, string kindId, int serial)
        {
            return $"{Prefix(ownerId, kindId, serial)} piloting started";
        }

        public static string PilotEnded(string ownerId, string kindId, int serial, DronePilotEnd reason)
        {
            return $"{Prefix(ownerId, kindId, serial)} piloting ended reason={reason}";
        }

        public static string Recalled(string ownerId, string kindId, int serial, Vector3 at)
        {
            return $"{Prefix(ownerId, kindId, serial)} recalled at={Point(at)}";
        }

        public static string PilotRefused(string ownerId, string kindId, int serial, string missing)
        {
            return $"{Prefix(ownerId, kindId, serial)} piloting refused missing={Name(missing)}";
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
