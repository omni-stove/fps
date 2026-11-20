using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // 置いた・消えた・撃たれて止めたは DeployLog に出す。ここは復活の場所として登録したことと、そこから復活させたことだけ。
    public static class BeaconLog
    {
        public const string Tag = "[Beacon]";

        public static string Prefix(string ownerId, string kindId, int serial)
        {
            return $"{Tag} owner={Name(ownerId)} kind={Name(kindId)} serial={serial}";
        }

        public static string Armed(string ownerId, string kindId, int serial, int uses, Side? side)
        {
            return $"{Prefix(ownerId, kindId, serial)} armed uses={uses} side={SideName(side)}";
        }

        public static string Used(string ownerId, string kindId, int serial, string userId, Vector3 at, int usesLeft, int uses)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} used by={1} at=({2:0.00}, {3:0.00}, {4:0.00}) uses={5}/{6}",
                Prefix(ownerId, kindId, serial),
                Name(userId),
                Clean(at.X),
                Clean(at.Y),
                Clean(at.Z),
                usesLeft,
                uses);
        }

        // 向きの計算で出る -0 や極小の負の値が "-0.00" と出ないよう、桁を丸めてから 0 を足して符号を消す。
        private static float Clean(float v)
        {
            return System.MathF.Round(v * 100f) / 100f + 0f;
        }

        private static string SideName(Side? side)
        {
            return side.HasValue ? side.Value.Id.ToString(CultureInfo.InvariantCulture) : "-";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
