using System;

namespace Shooter.Core
{
    // 倒れた人が落とす物の調整値。既定はオフラインの DropOnDeath (予備弾 30・30 秒で消える・0.75 m の円に並べる) と、
    // 倒れた人の手の武器を足元に置くオフラインの武器の落とし方 (Pickup.DroppedWeaponOffset・Look で拾う) に揃える
    public sealed class PickupDropSpec
    {
        public const bool DefaultDropsWeapon = true;
        public const int DefaultReserveAmmo = 30;
        public const float DefaultLifetimeSeconds = 30f;
        public const float DefaultReach = 1f;
        public const float DefaultSpreadRadius = 0.75f;

        public PickupDropSpec(
            bool dropsWeapon = DefaultDropsWeapon,
            int reserveAmmo = DefaultReserveAmmo,
            float lifetimeSeconds = DefaultLifetimeSeconds,
            float reach = DefaultReach,
            float spreadRadius = DefaultSpreadRadius)
        {
            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            SpecGuard.NonNegativeFinite(lifetimeSeconds, nameof(lifetimeSeconds));
            SpecGuard.NonNegativeFinite(spreadRadius, nameof(spreadRadius));
            if (!(reach > 0f) || float.IsInfinity(reach))
            {
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "reach must be positive and finite");
            }

            DropsWeapon = dropsWeapon;
            ReserveAmmo = reserveAmmo;
            LifetimeSeconds = lifetimeSeconds;
            Reach = reach;
            SpreadRadius = spreadRadius;
        }

        public static PickupDropSpec Default { get; } = new PickupDropSpec();

        // 手に持っていた武器を、そのときの弾数と付けた物のまま落とす。拾うのは拾う要求で (触れただけでは持ち替えない)
        public bool DropsWeapon { get; }

        // 触れたら今の武器に足す予備弾。0 なら落とさない
        public int ReserveAmmo { get; }

        // 0 なら消えない
        public float LifetimeSeconds { get; }

        public float Reach { get; }

        public float SpreadRadius { get; }

        public bool DropsAnything => DropsWeapon || ReserveAmmo > 0;
    }
}
