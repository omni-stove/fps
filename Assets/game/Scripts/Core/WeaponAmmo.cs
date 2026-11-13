using System;

namespace Fps.Core
{
    /// <summary>武器 1 本ぶんの残弾。セーブデータで武器の Id ごとに持つ。不変。</summary>
    public sealed class WeaponAmmo
    {
        /// <param name="weaponId">武器の Id (<see cref="WeaponSpec.Id"/>)。空にはできない。</param>
        /// <param name="ammoInMagazine">弾倉の弾数。0 以上。</param>
        /// <param name="reserveAmmo">予備弾の数。0 以上。</param>
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

        /// <summary>武器の Id。</summary>
        public string WeaponId { get; }

        /// <summary>弾倉の弾数。0 以上。</summary>
        public int AmmoInMagazine { get; }

        /// <summary>予備弾の数。0 以上。</summary>
        public int ReserveAmmo { get; }

        /// <summary>weapon の今の弾数を写す。</summary>
        public static WeaponAmmo Of(Weapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return new WeaponAmmo(weapon.Spec.Id, weapon.AmmoInMagazine, weapon.ReserveAmmo);
        }
    }
}
