using System;

namespace Fps.Core
{
    /// <summary>
    /// 乗り物に付いた武器 1 つの設定 (<see cref="VehicleSpec.Weapons"/> の 1 つ)。生成時に値を検査し、以後は変わらない。
    /// 武器そのものの性能は <see cref="WeaponSpec"/> を Id で引く。角度は乗り物の正面から測る度。
    /// </summary>
    public sealed class VehicleWeaponSpec
    {
        public const float DefaultYawMinDegrees = -180f;
        public const float DefaultYawMaxDegrees = 180f;
        public const float DefaultPitchMinDegrees = -10f;
        public const float DefaultPitchMaxDegrees = 30f;
        public const float DefaultTurnSpeedDegreesPerSecond = 90f;

        /// <param name="weaponId">使う武器の Id (<see cref="WeaponSpec"/> を引く)。空でない。</param>
        /// <param name="seatIndex">この武器を操作する座席の番号 (<see cref="VehicleSpec.Seats"/> の添字)。0 以上。</param>
        /// <param name="yawMinDegrees">左右に向けられる範囲の左端 (度)。-180 以上 yawMaxDegrees 以下。</param>
        /// <param name="yawMaxDegrees">左右に向けられる範囲の右端 (度)。180 以下。</param>
        /// <param name="pitchMinDegrees">上下に向けられる範囲の下端 (度)。-90 以上 pitchMaxDegrees 以下。</param>
        /// <param name="pitchMaxDegrees">上下に向けられる範囲の上端 (度)。90 以下。</param>
        /// <param name="turnSpeedDegreesPerSecond">向きを変える速さ (度/秒)。0 より大きい有限値。</param>
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
