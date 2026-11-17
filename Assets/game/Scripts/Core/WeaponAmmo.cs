using System;

namespace Shooter.Core
{
    public sealed class WeaponAmmo
    {
        public WeaponAmmo(string weaponId, int ammoInMagazine, int reserveAmmo)
        {
            if (weaponId == null)
            {
                throw new ArgumentNullException(nameof(weaponId));
            }

            if (weaponId.Length == 0)
            {
                throw new ArgumentException("weaponId must not be empty", nameof(weaponId));
            }

            if (ammoInMagazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must not be negative");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            WeaponId = weaponId;
            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
        }

        public string WeaponId { get; }

        public int AmmoInMagazine { get; }

        public int ReserveAmmo { get; }

        public static WeaponAmmo Of(Weapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return new WeaponAmmo(weapon.Spec.Id, weapon.AmmoInMagazine, weapon.ReserveAmmo);
        }

        // 手放す武器に付けていく弾。予備弾を持ち物から出し入れする武器は、予備弾が持ち物に残るので弾倉の分だけ
        public static WeaponAmmo Leaving(Weapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return new WeaponAmmo(weapon.Spec.Id, weapon.AmmoInMagazine, weapon.AmmoPool != null ? 0 : weapon.ReserveAmmo);
        }
    }
}
