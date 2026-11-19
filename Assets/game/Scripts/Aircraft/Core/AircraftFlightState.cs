using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct AircraftFlightState : IEquatable<AircraftFlightState>
    {
        public AircraftFlightState(float airspeed, float angleOfAttackRadians, float sideslipRadians, float loadFactor, bool grounded)
        {
            Airspeed = airspeed;
            AngleOfAttackRadians = angleOfAttackRadians;
            SideslipRadians = sideslipRadians;
            LoadFactor = loadFactor;
            Grounded = grounded;
        }

        public float Airspeed { get; }

        // 機首より下から空気を受けるときを正にするのは、機首を上げて翼が持ち上げる向きを正とする航空の慣例に合わせるため。
        public float AngleOfAttackRadians { get; }

        public float SideslipRadians { get; }

        public float LoadFactor { get; }

        public bool Grounded { get; }

        // 地面に対する速さを対気速度にするのは、キットにまだ風の入力が無いため。
        public static AircraftFlightState From(Vector3 localVelocity, Vector3 localAcceleration, float gravity, bool grounded)
        {
            if (!float.IsFinite(localVelocity.X) || !float.IsFinite(localVelocity.Y) || !float.IsFinite(localVelocity.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(localVelocity), localVelocity, "localVelocity must be finite");
            }

            if (!float.IsFinite(localAcceleration.X) || !float.IsFinite(localAcceleration.Y) || !float.IsFinite(localAcceleration.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(localAcceleration), localAcceleration, "localAcceleration must be finite");
            }

            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be a non-negative finite number");
            }

            var airspeed = localVelocity.Length();
            var angleOfAttack = (float)Math.Atan2(-localVelocity.Y, localVelocity.Z);
            var sideslip = airspeed > 0f ? (float)Math.Asin(Math.Clamp(localVelocity.X / airspeed, -1f, 1f)) : 0f;

            // 重さが無いときに 0 とするのは、割ると無限大になり、表示や判定に流すと扱えないため。
            var loadFactor = gravity > 0f ? localAcceleration.Y / gravity : 0f;
            return new AircraftFlightState(airspeed, angleOfAttack, sideslip, loadFactor, grounded);
        }

        public bool Equals(AircraftFlightState other)
        {
            return Airspeed.Equals(other.Airspeed)
                && AngleOfAttackRadians.Equals(other.AngleOfAttackRadians)
                && SideslipRadians.Equals(other.SideslipRadians)
                && LoadFactor.Equals(other.LoadFactor)
                && Grounded == other.Grounded;
        }

        public override bool Equals(object obj)
        {
            return obj is AircraftFlightState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Airspeed, AngleOfAttackRadians, SideslipRadians, LoadFactor, Grounded);
        }

        public static bool operator ==(AircraftFlightState left, AircraftFlightState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AircraftFlightState left, AircraftFlightState right)
        {
            return !left.Equals(right);
        }
    }
}
