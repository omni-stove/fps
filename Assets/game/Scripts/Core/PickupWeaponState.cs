using System;

namespace Fps.Core
{
    /// <summary><see cref="PickupCollector"/> が持っている武器 1 本の、拾う判定に要る状態。</summary>
    public readonly struct PickupWeaponState
    {
        /// <param name="weaponId">武器の Id。空でないこと。</param>
        /// <param name="reserveAmmo">今の予備弾。0 以上。</param>
        /// <param name="maxReserveAmmo">予備弾の上限。0 以上。</param>
        /// <param name="hasInfiniteReserve">予備弾が減らないなら true (拾っても増やす意味が無い)。</param>
        public PickupWeaponState(string weaponId, int reserveAmmo, int maxReserveAmmo, bool hasInfiniteReserve)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                throw new ArgumentException("weaponId must not be null or empty", nameof(weaponId));
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            if (maxReserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReserveAmmo), maxReserveAmmo, "maxReserveAmmo must not be negative");
            }

            WeaponId = weaponId;
            ReserveAmmo = reserveAmmo;
            MaxReserveAmmo = maxReserveAmmo;
            HasInfiniteReserve = hasInfiniteReserve;
        }

        public string WeaponId { get; }

        public int ReserveAmmo { get; }

        public int MaxReserveAmmo { get; }

        public bool HasInfiniteReserve { get; }

        /// <summary>予備弾をあと何発拾えるか。無限なら 0。</summary>
        public int Room => HasInfiniteReserve ? 0 : Math.Max(0, MaxReserveAmmo - ReserveAmmo);

        /// <summary>weapon の今の状態。</summary>
        public static PickupWeaponState Of(Weapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return new PickupWeaponState(weapon.Spec.Id, weapon.ReserveAmmo, weapon.MaxReserveAmmo, weapon.HasInfiniteReserve);
        }
    }
}
