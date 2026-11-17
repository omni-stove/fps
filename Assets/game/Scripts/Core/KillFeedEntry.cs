using System;

namespace Shooter.Core
{
    public readonly struct KillFeedEntry : IEquatable<KillFeedEntry>
    {
        // default(KillFeedEntry) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _weaponId;

        public int KillerId { get; }

        public int VictimId { get; }

        public string WeaponId => _weaponId ?? string.Empty;

        public bool Headshot { get; }

        public bool Friendly { get; }

        public KillFeedEntry(int killerId, int victimId, string weaponId, bool headshot, bool friendly)
        {
            KillerId = killerId;
            VictimId = victimId;
            _weaponId = weaponId ?? string.Empty;
            Headshot = headshot;
            Friendly = friendly;
        }

        public bool IsSuicide => KillerId == VictimId;

        public bool Equals(KillFeedEntry other)
        {
            return KillerId == other.KillerId
                && VictimId == other.VictimId
                && string.Equals(WeaponId, other.WeaponId, StringComparison.Ordinal)
                && Headshot == other.Headshot
                && Friendly == other.Friendly;
        }

        public override bool Equals(object obj)
        {
            return obj is KillFeedEntry other && Equals(other);
        }

        public static bool operator ==(KillFeedEntry left, KillFeedEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KillFeedEntry left, KillFeedEntry right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(KillerId, VictimId, StringComparer.Ordinal.GetHashCode(WeaponId), Headshot, Friendly);
        }

        public override string ToString()
        {
            return "killer=" + KillerId + " victim=" + VictimId + " weapon=" + WeaponId
                + " headshot=" + Headshot + " friendly=" + Friendly;
        }
    }
}
