using System;

namespace Shooter.Core
{
    public sealed class VehicleDriveSpec
    {
        public const float DefaultMaxSpeed = 40f;

        public const float DefaultCorrectionDistance = 1f;

        public const float DefaultBurstSeconds = 0.5f;

        // 最高速度ちょうどで断ると、坂を下る・落ちる・ぶつかって弾かれた速さで直してしまうので、その分を上乗せして受け入れる。
        public const float DefaultTopSpeedMargin = 1.5f;

        public static readonly VehicleDriveSpec Default = new VehicleDriveSpec();

        public VehicleDriveSpec(
            float maxSpeed = DefaultMaxSpeed, float correctionDistance = DefaultCorrectionDistance, float burstSeconds = DefaultBurstSeconds)
        {
            if (!(maxSpeed > 0f) || float.IsInfinity(maxSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed), maxSpeed, "maxSpeed must be positive and finite");
            }

            if (!(correctionDistance >= 0f) || float.IsInfinity(correctionDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(correctionDistance), correctionDistance, "correctionDistance must not be negative and must be finite");
            }

            if (!(burstSeconds > 0f) || float.IsInfinity(burstSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(burstSeconds), burstSeconds, "burstSeconds must be positive and finite");
            }

            MaxSpeed = maxSpeed;
            CorrectionDistance = correctionDistance;
            BurstSeconds = burstSeconds;
        }

        public float MaxSpeed { get; }

        public float CorrectionDistance { get; }

        public float BurstSeconds { get; }

        public static VehicleDriveSpec FromTopSpeed(float topSpeed, float margin = DefaultTopSpeedMargin)
        {
            if (!(topSpeed > 0f) || float.IsInfinity(topSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(topSpeed), topSpeed, "topSpeed must be positive and finite");
            }

            if (!(margin >= 1f) || float.IsInfinity(margin))
            {
                throw new ArgumentOutOfRangeException(nameof(margin), margin, "margin must be at least 1 and finite");
            }

            return new VehicleDriveSpec(topSpeed * margin);
        }

        public float MaxAllowance => (MaxSpeed * BurstSeconds) + CorrectionDistance;
    }
}
