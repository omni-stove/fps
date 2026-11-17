using System;
using System.Globalization;

namespace Shooter.Core
{
    // Core に表示の文を持たせないよう、名前は言語の表のキーで持ち、文にするのは UI に任せる。
    public readonly struct StreakSlot : IEquatable<StreakSlot>
    {
        // default(StreakSlot) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _rewardId;
        private readonly string _displayNameKey;

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
