using System;
using System.Numerics;

namespace Shooter.Core
{
    // 届いた間隔ではなく貯めた距離で測るのは、落ちてよい届け方の位置が遅れてまとめて着いても、速く送り続けて先へ進めないようにするため。
    public sealed class VehicleDriveCheck
    {
        public VehicleDriveCheck(VehicleDriveSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Allowance = spec.MaxAllowance;
        }

        public VehicleDriveSpec Spec { get; }

        public float Allowance { get; private set; }

        public void Refill(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            Allowance = Math.Min(Spec.MaxAllowance, Allowance + (Spec.MaxSpeed * deltaSeconds));
        }

        public VehicleMoveRejection Check(Vector3 from, Vector3 to)
        {
            var distance = Vector3.Distance(from, to);
            if (!(distance <= Allowance))
            {
                return VehicleMoveRejection.TooFast;
            }

            Allowance -= distance;
            return VehicleMoveRejection.None;
        }

        public void Reset()
        {
            Allowance = Spec.MaxAllowance;
        }

        public Vector3 ClampVelocity(Vector3 velocity)
        {
            var speed = velocity.Length();
            if (!float.IsFinite(speed))
            {
                return Vector3.Zero;
            }

            return speed > Spec.MaxSpeed ? velocity * (Spec.MaxSpeed / speed) : velocity;
        }
    }
}
