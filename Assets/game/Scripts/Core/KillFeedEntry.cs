using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// キルフィードの 1 行。誰が誰をどの武器で倒したか。
    /// </summary>
    public readonly struct KillFeedEntry : IEquatable<KillFeedEntry>
    {
        private static readonly int[] _noAssisters = Array.Empty<int>();

        // default(KillFeedEntry) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _weaponId;

        // default(KillFeedEntry) ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly int[] _assisterIds;

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

        /// <summary>アシストした Id (<see cref="DamageLedger.TakeAssisters"/> の並び)。いなければ空。null にならない。</summary>
        public IReadOnlyList<int> AssisterIds => _assisterIds ?? _noAssisters;

        /// <param name="killerId">倒したプレイヤーの Id。</param>
        /// <param name="victimId">倒されたプレイヤーの Id。</param>
        /// <param name="weaponId">武器の Id。null なら空文字 (武器によらない死)。</param>
        /// <param name="headshot">頭への止めか。</param>
        /// <param name="friendly">味方どうしか。</param>
        /// <param name="assisterIds">アシストした Id。null なら空。渡した後に元の一覧を変えても、この行は変わらない。</param>
        public KillFeedEntry(int killerId, int victimId, string weaponId, bool headshot, bool friendly, IReadOnlyList<int> assisterIds = null)
        {
            KillerId = killerId;
            VictimId = victimId;
            _weaponId = weaponId ?? string.Empty;
            Headshot = headshot;
            Friendly = friendly;
            _assisterIds = Copy(assisterIds);
        }

        /// <summary>自滅か (<see cref="KillerId"/> と <see cref="VictimId"/> が同じ)。</summary>
        public bool IsSuicide => KillerId == VictimId;

        public bool Equals(KillFeedEntry other)
        {
            return KillerId == other.KillerId
                && VictimId == other.VictimId
                && string.Equals(WeaponId, other.WeaponId, StringComparison.Ordinal)
                && Headshot == other.Headshot
                && Friendly == other.Friendly
                && SameAssisters(AssisterIds, other.AssisterIds);
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
            var assisters = new HashCode();
            foreach (var id in AssisterIds)
            {
                assisters.Add(id);
            }

            return HashCode.Combine(
                KillerId, VictimId, StringComparer.Ordinal.GetHashCode(WeaponId), Headshot, Friendly, assisters.ToHashCode());
        }

        public override string ToString()
        {
            // アシストの無い行は、アシストを足す前と同じ文字列のままにして、それを読むログやテストを変えずに済ませる。
            var assisters = AssisterIds.Count == 0 ? string.Empty : " assisters=" + string.Join(",", AssisterIds);
            return "killer=" + KillerId + " victim=" + VictimId + " weapon=" + WeaponId
                + " headshot=" + Headshot + " friendly=" + Friendly + assisters;
        }

        private static int[] Copy(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return _noAssisters;
            }

            var copy = new int[ids.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = ids[i];
            }

            return copy;
        }

        private static bool SameAssisters(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
