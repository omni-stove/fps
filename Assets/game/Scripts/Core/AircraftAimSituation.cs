using System;
using System.Numerics;

namespace Shooter.Core
{
    // 既定値 (default) を「何も分からない」にするのは、上昇率 0 なら地面までの時間が無限になり、G も迎え角も 0 で何も絞らないため。
    public readonly struct AircraftAimSituation : IEquatable<AircraftAimSituation>
    {
        public AircraftAimSituation(AircraftFlightState state, Vector3 localUp, float climbRate, float groundDistance)
        {
            if (!float.IsFinite(state.LoadFactor) || !float.IsFinite(state.AngleOfAttackRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "state must have a finite load factor and angle of attack");
            }

            if (!float.IsFinite(localUp.X) || !float.IsFinite(localUp.Y) || !float.IsFinite(localUp.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(localUp), localUp, "localUp must be finite");
            }

            if (!float.IsFinite(climbRate))
            {
                throw new ArgumentOutOfRangeException(nameof(climbRate), climbRate, "climbRate must be finite");
            }

            // 無限大を許すのは、下に地面が見つからないときをそのまま渡せるようにするため。
            if (!(groundDistance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(groundDistance), groundDistance, "groundDistance must be zero or more");
            }

            State = state;
            LocalUp = localUp;
            ClimbRate = climbRate;
            GroundDistance = groundDistance;
        }

        public AircraftFlightState State { get; }

        // 真上を機体から見た向きで持つのは、引き起こすときに機首を寄せる先を、視線と同じ当て方で扱えるようにするため。
        public Vector3 LocalUp { get; }

        public float ClimbRate { get; }

        public float GroundDistance { get; }

        public float SinkRate => Math.Max(0f, -ClimbRate);

        public float SecondsToGround => SinkRate > 0f ? GroundDistance / SinkRate : float.PositiveInfinity;

        public bool Equals(AircraftAimSituation other)
        {
            return State == other.State
                && LocalUp.Equals(other.LocalUp)
                && ClimbRate.Equals(other.ClimbRate)
                && GroundDistance.Equals(other.GroundDistance);
        }

        public override bool Equals(object obj)
        {
            return obj is AircraftAimSituation other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(State, LocalUp, ClimbRate, GroundDistance);
        }

        public static bool operator ==(AircraftAimSituation left, AircraftAimSituation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AircraftAimSituation left, AircraftAimSituation right)
        {
            return !left.Equals(right);
        }
    }
}
