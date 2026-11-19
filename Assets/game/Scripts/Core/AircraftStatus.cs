using System;

namespace Shooter.Core
{
    public readonly struct AircraftStatus : IEquatable<AircraftStatus>
    {
        private const float _radiansToDegrees = (float)(180.0 / Math.PI);

        public AircraftStatus(
            float speed,
            float altitude,
            float throttle,
            float angleOfAttackDegrees,
            float loadFactor,
            bool stallWarning,
            bool grounded)
        {
            RequireFinite(speed, nameof(speed));
            RequireFinite(altitude, nameof(altitude));
            RequireFinite(throttle, nameof(throttle));
            RequireFinite(angleOfAttackDegrees, nameof(angleOfAttackDegrees));
            RequireFinite(loadFactor, nameof(loadFactor));
            if (speed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative");
            }

            Speed = speed;
            Altitude = altitude;
            Throttle = throttle;
            AngleOfAttackDegrees = angleOfAttackDegrees;
            LoadFactor = loadFactor;
            StallWarning = stallWarning;
            Grounded = grounded;
        }

        public float Speed { get; }

        // 地面からの高さでなく世界の高さにするのは、地面を探る当たり判定を毎フレーム撃たずに済ませ、谷を越えても値が跳ねないようにするため。
        public float Altitude { get; }

        public float Throttle { get; }

        public float AngleOfAttackDegrees { get; }

        public float LoadFactor { get; }

        public bool StallWarning { get; }

        public bool Grounded { get; }

        // 地上で警告しないのは、止まって待つ間や滑走の出だしは遅くて当然で、警告が出続けると本当に要るときに目に入らなくなるため。
        public static AircraftStatus From(AircraftFlightState state, float altitude, float throttle, AircraftWarningSpec warning)
        {
            if (warning == null)
            {
                throw new ArgumentNullException(nameof(warning));
            }

            var angleOfAttack = state.AngleOfAttackRadians * _radiansToDegrees;
            var stall = !state.Grounded
                && (Math.Abs(angleOfAttack) >= warning.StallAngleDegrees || state.Airspeed < warning.StallSpeed);
            return new AircraftStatus(state.Airspeed, altitude, throttle, angleOfAttack, state.LoadFactor, stall, state.Grounded);
        }

        public bool Equals(AircraftStatus other)
        {
            return Speed.Equals(other.Speed)
                && Altitude.Equals(other.Altitude)
                && Throttle.Equals(other.Throttle)
                && AngleOfAttackDegrees.Equals(other.AngleOfAttackDegrees)
                && LoadFactor.Equals(other.LoadFactor)
                && StallWarning == other.StallWarning
                && Grounded == other.Grounded;
        }

        public override bool Equals(object obj)
        {
            return obj is AircraftStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Speed, Altitude, Throttle, AngleOfAttackDegrees, LoadFactor, StallWarning, Grounded);
        }

        public static bool operator ==(AircraftStatus left, AircraftStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AircraftStatus left, AircraftStatus right)
        {
            return !left.Equals(right);
        }

        private static void RequireFinite(float value, string name)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}
