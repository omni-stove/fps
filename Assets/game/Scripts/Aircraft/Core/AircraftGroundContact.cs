using System;
using System.Numerics;

namespace Shooter.Core
{
    // 機体から見た軸で持つのは、AircraftFlight が空力と同じ軸で足し合わせ、Runtime が AddRelativeForce にそのまま渡せるようにするため。
    public readonly struct AircraftGroundContact : IEquatable<AircraftGroundContact>
    {
        public AircraftGroundContact(
            bool grounded, Vector3 localAcceleration, Vector3 localAngularAcceleration, float? heightAboveGround = null)
        {
            RequireFinite(localAcceleration, nameof(localAcceleration));
            RequireFinite(localAngularAcceleration, nameof(localAngularAcceleration));
            if (heightAboveGround.HasValue && !(heightAboveGround.Value >= 0f && float.IsFinite(heightAboveGround.Value)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(heightAboveGround), heightAboveGround, "heightAboveGround must be a non-negative finite number or null");
            }

            Grounded = grounded;
            LocalAcceleration = localAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            HeightAboveGround = heightAboveGround;
        }

        public static AircraftGroundContact Airborne => default;

        public bool Grounded { get; }

        public Vector3 LocalAcceleration { get; }

        public Vector3 LocalAngularAcceleration { get; }

        // 地面が測れる範囲に無いときを 0 や大きな数でなく null にするのは、読む側が「真下に地面が無い」と「地面のすぐ上」を取り違えないようにするため。
        public float? HeightAboveGround { get; }

        public bool Equals(AircraftGroundContact other)
        {
            return Grounded == other.Grounded
                && LocalAcceleration.Equals(other.LocalAcceleration)
                && LocalAngularAcceleration.Equals(other.LocalAngularAcceleration)
                && Nullable.Equals(HeightAboveGround, other.HeightAboveGround);
        }

        public override bool Equals(object obj)
        {
            return obj is AircraftGroundContact other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Grounded, LocalAcceleration, LocalAngularAcceleration, HeightAboveGround);
        }

        public static bool operator ==(AircraftGroundContact left, AircraftGroundContact right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AircraftGroundContact left, AircraftGroundContact right)
        {
            return !left.Equals(right);
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}
