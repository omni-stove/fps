using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 座席の向きの範囲の外では撃たせない。照準の向きは入力ではなく武器の照準 (カメラ) から読む。撃つ弾はその向きへ飛ぶため。
    public sealed class OwnWeaponSeatModifier : IWeaponModifier
    {
        public OwnWeaponSeatModifier(VehicleSeat seat, Transform aim)
        {
            Seat = seat != null ? seat : throw new ArgumentNullException(nameof(seat));
            Aim = aim != null ? aim : throw new ArgumentNullException(nameof(aim));
        }

        public VehicleSeat Seat { get; }

        public Transform Aim { get; }

        public float YawOffsetDegrees
        {
            get
            {
                var forward = Aim.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-8f)
                {
                    return 0f;
                }

                var yaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
                return Mathf.DeltaAngle(Seat.SitYawDegrees, yaw);
            }
        }

        // 座席ごと消えた後に外し忘れても、撃てないままにはしない。
        public bool BlocksFire => Seat != null && Aim != null && !Seat.Spec.AllowsOwnWeaponAt(YawOffsetDegrees);

        public bool BlocksReload => false;

        public float SpreadMultiplier => Seat != null ? Seat.Spec.OwnWeaponSpreadMultiplier : 1f;

        public float ModifyConeDegrees(WeaponSpec spec, float coneDegrees)
        {
            return coneDegrees;
        }

        public float ModifySpreadFraction(float spreadFraction)
        {
            return spreadFraction;
        }
    }
}
