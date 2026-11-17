using System;

namespace Shooter.Core
{
    public sealed class VehicleWeaponSpec
    {
        public const float DefaultYawMinDegrees = -180f;
        public const float DefaultYawMaxDegrees = 180f;
        public const float DefaultPitchMinDegrees = -10f;
        public const float DefaultPitchMaxDegrees = 30f;
        public const float DefaultTurnSpeedDegreesPerSecond = 90f;

        public VehicleWeaponSpec(
            string weaponId,
            int seatIndex,
            float yawMinDegrees = DefaultYawMinDegrees,
            float yawMaxDegrees = DefaultYawMaxDegrees,
            float pitchMinDegrees = DefaultPitchMinDegrees,
            float pitchMaxDegrees = DefaultPitchMaxDegrees,
            float turnSpeedDegreesPerSecond = DefaultTurnSpeedDegreesPerSecond)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException("weaponId must not be empty", nameof(weaponId));
            }

            if (seatIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seatIndex), seatIndex, "seatIndex must not be negative");
            }

            if (!(yawMinDegrees >= -180f && yawMinDegrees <= yawMaxDegrees && yawMaxDegrees <= 180f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(yawMinDegrees), yawMinDegrees, $"yaw range must satisfy -180 <= min <= max <= 180 (max={yawMaxDegrees})");
            }

            if (!(pitchMinDegrees >= -90f && pitchMinDegrees <= pitchMaxDegrees && pitchMaxDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pitchMinDegrees), pitchMinDegrees, $"pitch range must satisfy -90 <= min <= max <= 90 (max={pitchMaxDegrees})");
            }

            if (!(turnSpeedDegreesPerSecond > 0f) || float.IsInfinity(turnSpeedDegreesPerSecond))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnSpeedDegreesPerSecond), turnSpeedDegreesPerSecond, "turnSpeedDegreesPerSecond must be positive and finite");
            }

            WeaponId = weaponId;
            SeatIndex = seatIndex;
            YawMinDegrees = yawMinDegrees;
            YawMaxDegrees = yawMaxDegrees;
            PitchMinDegrees = pitchMinDegrees;
            PitchMaxDegrees = pitchMaxDegrees;
            TurnSpeedDegreesPerSecond = turnSpeedDegreesPerSecond;
        }

        public string WeaponId { get; }

        public int SeatIndex { get; }

        public float YawMinDegrees { get; }

        public float YawMaxDegrees { get; }

        public float PitchMinDegrees { get; }

        public float PitchMaxDegrees { get; }

        public float TurnSpeedDegreesPerSecond { get; }
    }
}
