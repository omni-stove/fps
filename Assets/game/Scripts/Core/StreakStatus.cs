using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct StreakStatus : IEquatable<StreakStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly StreakSlot[] _slots;

        public StreakStatus(int kills, IReadOnlyList<StreakSlot> slots)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            Kills = kills;
            var copy = new StreakSlot[slots?.Count ?? 0];
            var ready = 0;
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = slots[i];
                if (copy[i].Ready)
                {
                    ready++;
                }
            }

            _slots = copy;
            ReadyCount = ready;
        }

        public int Kills { get; }

        public IReadOnlyList<StreakSlot> Slots => _slots ?? Array.Empty<StreakSlot>();

        public int ReadyCount { get; }

        public bool Equals(StreakStatus other)
        {
            if (Kills != other.Kills || ReadyCount != other.ReadyCount)
            {
                return false;
            }

            var mine = Slots;
            var theirs = other.Slots;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (!mine[i].Equals(theirs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is StreakStatus other && Equals(other);
        }

        public static bool operator ==(StreakStatus left, StreakStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StreakStatus left, StreakStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Kills, ReadyCount);
            foreach (var slot in Slots)
            {
                hash = HashCode.Combine(hash, slot);
            }

            return hash;
        }

        public override string ToString()
        {
            return "kills " + Kills.ToString(CultureInfo.InvariantCulture)
                + " ready " + ReadyCount.ToString(CultureInfo.InvariantCulture)
                + "/" + Slots.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
