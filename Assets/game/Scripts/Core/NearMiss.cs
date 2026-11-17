using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class NearMiss
    {
        // 撃った者の背中側は数えない (撃ち始めた位置のすぐ後ろの味方まで制圧しないように)。
        // 弾が止まった先は止まった点からの距離にする。すぐ横の壁に弾が当たっても制圧に数えたいので。
        public static bool TryDistance(Vector3 origin, Vector3 direction, float length, Vector3 point, out float distance, out float along)
        {
            if (direction.LengthSquared() < 1e-12f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            if (!(length >= 0f) || float.IsInfinity(length))
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "length must be non-negative and finite");
            }

            var unit = Vector3.Normalize(direction);
            var t = Vector3.Dot(point - origin, unit);
            if (!(t >= 0f))
            {
                distance = 0f;
                along = 0f;
                return false;
            }

            along = Math.Min(t, length);
            distance = Vector3.Distance(origin + (unit * along), point);
            return true;
        }
    }
}
