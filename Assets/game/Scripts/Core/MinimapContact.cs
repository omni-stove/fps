using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct MinimapContact : IEquatable<MinimapContact>
    {
        public MinimapContact(MinimapContactKind kind, Vector3 position, float headingDegrees, Team? team)
        {
            if (!Enum.IsDefined(typeof(MinimapContactKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a defined MinimapContactKind");
            }

            if (team.HasValue && !Enum.IsDefined(typeof(Team), team.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "team must be a defined Team");
            }

            Kind = kind;
            Position = RequireFinite(position, nameof(position));
            HeadingDegrees = NormalizeHeading(headingDegrees, nameof(headingDegrees));
            Team = team;
        }

        public MinimapContactKind Kind { get; }

        public Vector3 Position { get; }

        public float HeadingDegrees { get; }

        public Team? Team { get; }

        public bool Equals(MinimapContact other)
        {
            return Kind == other.Kind
                && Position.Equals(other.Position)
                && HeadingDegrees.Equals(other.HeadingDegrees)
                && Team == other.Team;
        }

        public override bool Equals(object obj)
        {
            return obj is MinimapContact other && Equals(other);
        }

        public static bool operator ==(MinimapContact left, MinimapContact right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MinimapContact left, MinimapContact right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, Position, HeadingDegrees, Team);
        }

        public override string ToString()
        {
            return Kind + " " + DescribePose(Position, HeadingDegrees) + (Team.HasValue ? " " + Team.Value : string.Empty);
        }

        internal static Vector3 RequireFinite(Vector3 position, string paramName)
        {
            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, position, paramName + " must be finite");
            }

            return position;
        }

        // 359.9 と -0.1 のような同じ向きを同じ値にして、等しさの比較と UI の回転を 1 通りにする。
        internal static float NormalizeHeading(float degrees, string paramName)
        {
            if (!IsFinite(degrees))
            {
                throw new ArgumentOutOfRangeException(paramName, degrees, paramName + " must be finite");
            }

            var wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            // -0 も 0 に寄せるのは、ハッシュが 0 と分かれないようにするため。
            return wrapped >= 360f || wrapped == 0f ? 0f : wrapped;
        }

        internal static string DescribePose(Vector3 position, float headingDegrees)
        {
            return "(" + position.X.ToString("0.0", CultureInfo.InvariantCulture)
                + ", " + position.Y.ToString("0.0", CultureInfo.InvariantCulture)
                + ", " + position.Z.ToString("0.0", CultureInfo.InvariantCulture)
                + ") " + headingDegrees.ToString("0", CultureInfo.InvariantCulture) + "deg";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
