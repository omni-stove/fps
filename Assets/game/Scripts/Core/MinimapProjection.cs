using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class MinimapProjection
    {
        // 真上や真下に近い向きは水平の成分がわずかな揺れで大きく回るので、方位を出さずに無いと返す。
        private const float _minHorizontalLengthSquared = 1e-8f;

        private const float _degreesPerRadian = 180f / MathF.PI;

        public static Vector2 ToMap(MinimapSpec spec, Vector3 selfPosition, float selfHeadingDegrees, Vector3 worldPosition, out bool onEdge)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            MinimapContact.RequireFinite(selfPosition, nameof(selfPosition));
            MinimapContact.RequireFinite(worldPosition, nameof(worldPosition));
            var heading = MinimapContact.NormalizeHeading(selfHeadingDegrees, nameof(selfHeadingDegrees));

            var offset = new Vector2(worldPosition.X - selfPosition.X, worldPosition.Z - selfPosition.Z);
            var distance = offset.Length();
            onEdge = distance > spec.Radius;
            if (onEdge)
            {
                offset *= spec.Radius / distance;
            }

            if (spec.RotateWithPlayer)
            {
                offset = RotateCounterClockwise(offset, heading);
            }

            return offset * spec.Scale;
        }

        public static Vector2 ToMap(MinimapSpec spec, MinimapStatus status, MinimapContact contact, out bool onEdge)
        {
            return ToMap(spec, status.SelfPosition, status.SelfHeadingDegrees, contact.Position, out onEdge);
        }

        public static float MapHeadingDegrees(MinimapSpec spec, float selfHeadingDegrees, float headingDegrees)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var self = MinimapContact.NormalizeHeading(selfHeadingDegrees, nameof(selfHeadingDegrees));
            var heading = MinimapContact.NormalizeHeading(headingDegrees, nameof(headingDegrees));
            return spec.RotateWithPlayer ? MinimapContact.NormalizeHeading(heading - self, nameof(headingDegrees)) : heading;
        }

        public static bool TryHeadingDegrees(Vector3 direction, out float degrees)
        {
            MinimapContact.RequireFinite(direction, nameof(direction));
            if (direction.X * direction.X + direction.Z * direction.Z < _minHorizontalLengthSquared)
            {
                degrees = 0f;
                return false;
            }

            degrees = MinimapContact.NormalizeHeading(MathF.Atan2(direction.X, direction.Z) * _degreesPerRadian, nameof(direction));
            return true;
        }

        public static bool TryBearingDegrees(Vector3 from, Vector3 to, out float degrees)
        {
            MinimapContact.RequireFinite(from, nameof(from));
            MinimapContact.RequireFinite(to, nameof(to));
            return TryHeadingDegrees(to - from, out degrees);
        }

        public static float RelativeDegrees(float selfHeadingDegrees, float bearingDegrees)
        {
            var self = MinimapContact.NormalizeHeading(selfHeadingDegrees, nameof(selfHeadingDegrees));
            var bearing = MinimapContact.NormalizeHeading(bearingDegrees, nameof(bearingDegrees));
            var delta = MinimapContact.NormalizeHeading(bearing - self, nameof(bearingDegrees));
            return delta >= 180f ? delta - 360f : delta;
        }

        // 自分の前 (heading の向き) を地図の上 (+Y) へ持ってくるには、時計回りの heading の分だけ反時計回りに回す。
        private static Vector2 RotateCounterClockwise(Vector2 offset, float degrees)
        {
            var radians = degrees / _degreesPerRadian;
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            return new Vector2(offset.X * cos - offset.Y * sin, offset.X * sin + offset.Y * cos);
        }
    }
}
