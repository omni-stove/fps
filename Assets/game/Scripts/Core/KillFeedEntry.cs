using System;

namespace Fps.Core
{
    /// <summary>
    /// キルフィードの 1 行。誰が誰をどの武器で倒したか。
    /// </summary>
    public readonly struct KillFeedEntry : IEquatable<KillFeedEntry>
    {
        // default(KillFeedEntry) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _weaponId;

        /// <summary>倒したプレイヤーの Id。自滅 (落下など) なら <see cref="VictimId"/> と同じ。</summary>
        public int KillerId { get; }

        /// <summary>倒されたプレイヤーの Id。</summary>
        public int VictimId { get; }

        /// <summary>倒した武器の <see cref="WeaponSpec.Id"/>。武器によらない死 (出血・落下など) なら空文字。null にならない。</summary>
        public string WeaponId => _weaponId ?? string.Empty;

        /// <summary>止めの弾が頭に当たったか。</summary>
        public bool Headshot { get; }

        /// <summary>倒した側と倒された側が味方どうしか (味方撃ち)。</summary>
        public bool Friendly { get; }

        /// <param name="killerId">倒したプレイヤーの Id。</param>
        /// <param name="victimId">倒されたプレイヤーの Id。</param>
        /// <param name="weaponId">武器の Id。null なら空文字 (武器によらない死)。</param>
        /// <param name="headshot">頭への止めか。</param>
        /// <param name="friendly">味方どうしか。</param>
        public KillFeedEntry(int killerId, int victimId, string weaponId, bool headshot, bool friendly)
        {
            KillerId = killerId;
            VictimId = victimId;
            _weaponId = weaponId ?? string.Empty;
            Headshot = headshot;
            Friendly = friendly;
        }

        /// <summary>自滅か (<see cref="KillerId"/> と <see cref="VictimId"/> が同じ)。</summary>
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
