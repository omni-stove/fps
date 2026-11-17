using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>ストリークの報酬 1 つ分の枠。名前は文ではなく言語の表のキー (<see cref="DisplayNameKey"/>) で持ち、文にするのは UI。</summary>
    public readonly struct StreakSlot : IEquatable<StreakSlot>
    {
        // default(StreakSlot) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _rewardId;
        private readonly string _displayNameKey;

        /// <param name="rewardId">報酬の Id。</param>
        /// <param name="displayNameKey">報酬の名前の、言語の表のキー。無ければ null か空文字。</param>
        /// <param name="killsRequired">倒れずに倒す数。この数に届くと使えるようになる。</param>
        /// <param name="ready">今使えるか。</param>
        /// <exception cref="ArgumentException">rewardId が空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">killsRequired が 1 より小さい。</exception>
        public StreakSlot(string rewardId, string displayNameKey, int killsRequired, bool ready)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException("rewardId must not be empty", nameof(rewardId));
            }

            if (killsRequired < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(killsRequired), killsRequired, "killsRequired must be at least 1");
            }

            _rewardId = rewardId;
            _displayNameKey = displayNameKey ?? string.Empty;
            KillsRequired = killsRequired;
            Ready = ready;
        }

        public string RewardId => _rewardId ?? string.Empty;

        public string DisplayNameKey => _displayNameKey ?? string.Empty;

        public int KillsRequired { get; }

        public bool Ready { get; }

        public bool Equals(StreakSlot other)
        {
            return string.Equals(RewardId, other.RewardId, StringComparison.Ordinal)
                && string.Equals(DisplayNameKey, other.DisplayNameKey, StringComparison.Ordinal)
                && KillsRequired == other.KillsRequired
                && Ready == other.Ready;
        }

        public override bool Equals(object obj)
        {
            return obj is StreakSlot other && Equals(other);
        }

        public static bool operator ==(StreakSlot left, StreakSlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StreakSlot left, StreakSlot right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(RewardId), StringComparer.Ordinal.GetHashCode(DisplayNameKey), KillsRequired, Ready);
        }

        public override string ToString()
        {
            return RewardId + " " + KillsRequired.ToString(CultureInfo.InvariantCulture) + (Ready ? " ready" : string.Empty);
        }
    }
}
