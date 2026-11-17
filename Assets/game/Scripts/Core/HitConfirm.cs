using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct HitConfirm : IEquatable<HitConfirm>
    {
        public int Damage { get; }

        public HitZone Zone { get; }

        public bool Killed { get; }

        public bool Downed { get; }

        public Vector3? Point { get; }

        public HitConfirm(int damage, HitZone zone, bool killed, bool downed, Vector3? point = null)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!Enum.IsDefined(typeof(HitZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "zone is not a defined HitZone");
            }

            if (killed && downed)
            {
                throw new ArgumentException("a hit cannot both kill and down the target", nameof(downed));
            }

            Damage = damage;
            Zone = zone;
            Killed = killed;
            Downed = downed;
            Point = point;
        }

        public bool Equals(HitConfirm other)
        {
            return Damage == other.Damage && Zone == other.Zone && Killed == other.Killed && Downed == other.Downed && Point == other.Point;
        }

        public override bool Equals(object obj)
        {
            return obj is HitConfirm other && Equals(other);
        }

        public static bool operator ==(HitConfirm left, HitConfirm right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HitConfirm left, HitConfirm right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Damage, (int)Zone, Killed, Downed, Point);
        }

        public override string ToString()
        {
            return "damage=" + Damage + " zone=" + Zone + " killed=" + Killed + " downed=" + Downed;
        }
    }
}
