using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // 手放した武器を、拾った人がそのまま持てるよう、付けた物を外した元の値・付けていた物・弾数で持つ。
    // 予備弾を持ち物の弾薬から出し入れする武器は、予備弾が持ち物の方に残るので 0 にする
    internal sealed class ServerHeldWeapon
    {
        public ServerHeldWeapon(WeaponSpec spec, IReadOnlyList<string> attachments, int ammoInMagazine, int reserveAmmo)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Attachments = attachments ?? WeaponAttachments.None;
            if (ammoInMagazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must not be negative");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
        }

        public WeaponSpec Spec { get; }

        public IReadOnlyList<string> Attachments { get; }

        public int AmmoInMagazine { get; }

        public int ReserveAmmo { get; }
    }
}
