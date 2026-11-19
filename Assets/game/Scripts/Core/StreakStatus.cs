using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct StreakStatus : IEquatable<StreakStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly StreakSlot[] _slots;
        private readonly string _selectedRewardId;

        // 選んでいる報酬が使える枠に無いとき (渡さなかったときを含む) は、使える枠のうち先頭を選んでいるとみなす。
        // 使える報酬があれば必ずどれかを選んでいることにして、使うキーの行を出し忘れないため。
        public StreakStatus(int kills, IReadOnlyList<StreakSlot> slots, string selectedRewardId = null)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            Kills = kills;
            var copy = new StreakSlot[slots?.Count ?? 0];
            var ready = 0;
            string firstReady = null;
            string selected = null;
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = slots[i];
                if (!copy[i].Ready)
                {
                    continue;
                }

                ready++;
                firstReady ??= copy[i].RewardId;
                if (selected == null && string.Equals(copy[i].RewardId, selectedRewardId, StringComparison.Ordinal))
                {
                    selected = copy[i].RewardId;
                }
            }

            _slots = copy;
            ReadyCount = ready;
            _selectedRewardId = selected ?? firstReady;
        }

        public int Kills { get; }

        public IReadOnlyList<StreakSlot> Slots => _slots ?? Array.Empty<StreakSlot>();

        public int ReadyCount { get; }

        // 使える報酬が無ければ空文字。
        public string SelectedRewardId => _selectedRewardId ?? string.Empty;

        public bool IsSelected(StreakSlot slot)
        {
            return slot.Ready && ReadyCount > 0 && string.Equals(slot.RewardId, SelectedRewardId, StringComparison.Ordinal);
        }

        public bool Equals(StreakStatus other)
        {
            if (Kills != other.Kills || ReadyCount != other.ReadyCount || !string.Equals(SelectedRewardId, other.SelectedRewardId, StringComparison.Ordinal))
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
            var hash = HashCode.Combine(Kills, ReadyCount, StringComparer.Ordinal.GetHashCode(SelectedRewardId));
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
                + "/" + Slots.Count.ToString(CultureInfo.InvariantCulture)
                + (ReadyCount > 0 ? " selected " + SelectedRewardId : string.Empty);
        }
    }
}
