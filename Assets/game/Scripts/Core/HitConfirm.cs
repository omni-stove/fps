using System;

namespace Fps.Core
{
    /// <summary>
    /// 撃った本人に知らせる、弾が当たったことの確認。ヒットマーカーと命中音に使う。
    /// 生成時に値を検査する。
    /// </summary>
    public readonly struct HitConfirm : IEquatable<HitConfirm>
    {
        /// <summary>与えたダメージ。0 以上。</summary>
        public int Damage { get; }

        /// <summary>当たった部位。</summary>
        public HitZone Zone { get; }

        /// <summary>この弾で倒したか。</summary>
        public bool Killed { get; }

        /// <summary>この弾で倒さずにダウンさせたか。<see cref="Killed"/> と同時には true にならない。</summary>
        public bool Downed { get; }

        /// <param name="damage">与えたダメージ。0 以上。</param>
        /// <param name="zone">当たった部位。定義済みの値。</param>
        /// <param name="killed">倒したか。</param>
        /// <param name="downed">ダウンさせたか。killed と同時に true にはできない。</param>
        public HitConfirm(int damage, HitZone zone, bool killed, bool downed)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!Enum.IsDefined(typeof(HitZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "zone is not a defined HitZone");
            }

            if (killed && downed)
            {
                throw new ArgumentException("a hit cannot both kill and down the target", nameof(downed));
            }

            Damage = damage;
            Zone = zone;
            Killed = killed;
            Downed = downed;
        }

        public bool Equals(HitConfirm other)
        {
            return Damage == other.Damage && Zone == other.Zone && Killed == other.Killed && Downed == other.Downed;
        }

        public override bool Equals(object obj)
        {
            return obj is HitConfirm other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Damage, (int)Zone, Killed, Downed);
        }

        public override string ToString()
        {
            return "damage=" + Damage + " zone=" + Zone + " killed=" + Killed + " downed=" + Downed;
        }
    }
}
