using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public readonly struct KillFeedEntry : IEquatable<KillFeedEntry>
    {
        private static readonly int[] _noAssisters = Array.Empty<int>();

        // default(KillFeedEntry) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _weaponId;

        // default(KillFeedEntry) ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly int[] _assisterIds;

        public int KillerId { get; }

        public int VictimId { get; }

        public string WeaponId => _weaponId ?? string.Empty;

        public bool Headshot { get; }

        public bool Friendly { get; }

        public IReadOnlyList<int> AssisterIds => _assisterIds ?? _noAssisters;

        public KillFeedEntry(int killerId, int victimId, string weaponId, bool headshot, bool friendly, IReadOnlyList<int> assisterIds = null)
        {
            KillerId = killerId;
            VictimId = victimId;
            _weaponId = weaponId ?? string.Empty;
            Headshot = headshot;
            Friendly = friendly;
            _assisterIds = Copy(assisterIds);
        }

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
