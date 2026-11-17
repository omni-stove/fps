using System;

namespace Shooter.Core
{
    public readonly struct PickupWeaponState
    {
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

        // 予備弾が無限なら拾っても増やす意味が無いので、拾える余地を 0 にする。
        public int Room => HasInfiniteReserve ? 0 : Math.Max(0, MaxReserveAmmo - ReserveAmmo);

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
