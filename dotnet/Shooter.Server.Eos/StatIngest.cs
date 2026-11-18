using System;
using System.Globalization;

namespace Shooter.Server.Eos
{
    internal readonly struct StatIngest : IEquatable<StatIngest>
    {
        public StatIngest(string statName, int amount)
        {
            if (string.IsNullOrWhiteSpace(statName))
            {
                throw new ArgumentException("statName must not be empty", nameof(statName));
            }

            StatName = statName;
            Amount = amount;
        }

        public string StatName { get; }

        // EOS の IngestStat が受け取るのは 32 ビットの値なので、送る側で縮める。
        public int Amount { get; }

        public static int Clamp(long amount)
        {
            return amount > int.MaxValue ? int.MaxValue : amount < int.MinValue ? int.MinValue : (int)amount;
        }

        public bool Equals(StatIngest other)
        {
            return string.Equals(StatName, other.StatName, StringComparison.Ordinal) && Amount == other.Amount;
        }

        public override bool Equals(object obj)
        {
            return obj is StatIngest other && Equals(other);
        }

        public static bool operator ==(StatIngest left, StatIngest right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatIngest left, StatIngest right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StatName == null ? 0 : StringComparer.Ordinal.GetHashCode(StatName), Amount);
        }

        public override string ToString()
        {
            return StatName + "=" + Amount.ToString(CultureInfo.InvariantCulture);
        }
    }
}
