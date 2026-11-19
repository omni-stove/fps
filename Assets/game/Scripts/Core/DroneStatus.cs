using System;
using System.Globalization;

namespace Shooter.Core
{
    // 電池と範囲を秒やメートルでなく 0〜1 の割合で持つのは、上限がドローンの種類ごとのデータで決まり、UI はゲージを描くだけのため。
    public readonly struct DroneStatus : IEquatable<DroneStatus>
    {
        public DroneStatus(float batteryFraction, float rangeFraction, int spottedCount, bool piloting)
        {
            if (!(batteryFraction >= 0f && batteryFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(batteryFraction), batteryFraction, "batteryFraction must be between 0 and 1");
            }

            if (!(rangeFraction >= 0f && rangeFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(rangeFraction), rangeFraction, "rangeFraction must be between 0 and 1");
            }

            if (spottedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spottedCount), spottedCount, "spottedCount must not be negative");
            }

            BatteryFraction = batteryFraction;
            RangeFraction = rangeFraction;
            SpottedCount = spottedCount;
            Piloting = piloting;
        }

        public float BatteryFraction { get; }

        public float RangeFraction { get; }

        public int SpottedCount { get; }

        public bool Piloting { get; }

        public bool IsBatteryEmpty => BatteryFraction <= 0f;

        public bool Equals(DroneStatus other)
        {
            return BatteryFraction.Equals(other.BatteryFraction)
                && RangeFraction.Equals(other.RangeFraction)
                && SpottedCount == other.SpottedCount
                && Piloting == other.Piloting;
        }

        public override bool Equals(object obj)
        {
            return obj is DroneStatus other && Equals(other);
        }

        public static bool operator ==(DroneStatus left, DroneStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DroneStatus left, DroneStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BatteryFraction, RangeFraction, SpottedCount, Piloting);
        }

        public override string ToString()
        {
            return "battery=" + BatteryFraction.ToString("0.00", CultureInfo.InvariantCulture)
                + " range=" + RangeFraction.ToString("0.00", CultureInfo.InvariantCulture)
                + " spotted=" + SpottedCount.ToString(CultureInfo.InvariantCulture)
                + " piloting=" + (Piloting ? "yes" : "no");
        }
    }
}
