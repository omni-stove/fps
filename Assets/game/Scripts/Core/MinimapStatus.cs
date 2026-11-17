using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>ミニマップとコンパスに出すもの。自分の位置と向きは <see cref="MinimapContact"/> と同じ決め方。</summary>
    public readonly struct MinimapStatus : IEquatable<MinimapStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly MinimapContact[] _contacts;

        /// <param name="selfPosition">自分のワールドの位置。</param>
        /// <param name="selfHeadingDegrees">自分の向き (度)。0 以上 360 未満に寄せて持つ。</param>
        /// <param name="contacts">載せるもの。並びはそのまま持つ。null なら空。</param>
        /// <exception cref="ArgumentOutOfRangeException">selfPosition か selfHeadingDegrees が有限でない。</exception>
        public MinimapStatus(Vector3 selfPosition, float selfHeadingDegrees, IReadOnlyList<MinimapContact> contacts)
        {
            SelfPosition = MinimapContact.RequireFinite(selfPosition, nameof(selfPosition));
            SelfHeadingDegrees = MinimapContact.NormalizeHeading(selfHeadingDegrees, nameof(selfHeadingDegrees));
            var copy = new MinimapContact[contacts?.Count ?? 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = contacts[i];
            }

            _contacts = copy;
        }

        public Vector3 SelfPosition { get; }

        public float SelfHeadingDegrees { get; }

        public IReadOnlyList<MinimapContact> Contacts => _contacts ?? Array.Empty<MinimapContact>();

        public bool Equals(MinimapStatus other)
        {
            if (!SelfPosition.Equals(other.SelfPosition) || !SelfHeadingDegrees.Equals(other.SelfHeadingDegrees))
            {
                return false;
            }

            var mine = Contacts;
            var theirs = other.Contacts;
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
            return obj is MinimapStatus other && Equals(other);
        }

        public static bool operator ==(MinimapStatus left, MinimapStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MinimapStatus left, MinimapStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(SelfPosition, SelfHeadingDegrees);
            foreach (var contact in Contacts)
            {
                hash = HashCode.Combine(hash, contact);
            }

            return hash;
        }

        public override string ToString()
        {
            return "self " + MinimapContact.DescribePose(SelfPosition, SelfHeadingDegrees)
                + " contacts " + Contacts.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
