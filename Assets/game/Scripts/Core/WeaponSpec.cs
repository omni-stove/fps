using System;

namespace Fps.Core
{
    /// <summary>
    /// 武器の不変な設定。生成時に値を検査し、不正なら例外を投げる。
    /// </summary>
    public sealed class WeaponSpec
    {
        /// <summary>弾倉の容量。1 以上。</summary>
        public int MagazineSize { get; }

        /// <summary>予備弾の初期値。0 以上。</summary>
        public int ReserveAmmo { get; }

        /// <summary>射撃から次に撃てるまでの秒数。0 以上の有限値。</summary>
        public float FireInterval { get; }

        /// <summary>リロード開始から補充までの秒数。0 以上の有限値。</summary>
        public float ReloadSeconds { get; }

        /// <summary>1 発あたりのダメージ。0 以上。</summary>
        public int Damage { get; }

        /// <param name="magazineSize">弾倉の容量。1 以上。</param>
        /// <param name="reserveAmmo">予備弾の初期値。0 以上。</param>
        /// <param name="fireInterval">連射間隔 (秒)。0 以上の有限値。</param>
        /// <param name="reloadSeconds">リロード時間 (秒)。0 以上の有限値。</param>
        /// <param name="damage">1 発あたりのダメージ。0 以上。</param>
        public WeaponSpec(int magazineSize, int reserveAmmo, float fireInterval, float reloadSeconds, int damage)
        {
            if (magazineSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineSize), magazineSize, "magazineSize must be positive");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            if (!IsNonNegativeFinite(fireInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(fireInterval), fireInterval, "fireInterval must be a non-negative finite number");
            }

            if (!IsNonNegativeFinite(reloadSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadSeconds), reloadSeconds, "reloadSeconds must be a non-negative finite number");
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            MagazineSize = magazineSize;
            ReserveAmmo = reserveAmmo;
            FireInterval = fireInterval;
            ReloadSeconds = reloadSeconds;
            Damage = damage;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }
    }
}
