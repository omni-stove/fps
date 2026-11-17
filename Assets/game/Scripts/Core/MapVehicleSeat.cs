using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 位置はどれも乗り物の原点から見た向きと距離 (乗り物の回転を掛ける前) で持つ。乗り物が動いても書き直さずに済むようにするため。
    public sealed class MapVehicleSeat
    {
        public MapVehicleSeat(
            Vector3 sit, float sitYawDegrees = 0f, IReadOnlyList<Vector3> enterPoints = null, IReadOnlyList<Vector3> exitPoints = null)
        {
            Sit = RequireFinite(sit, nameof(sit));
            if (!float.IsFinite(sitYawDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(sitYawDegrees), sitYawDegrees, "sitYawDegrees must be finite");
            }

            SitYawDegrees = sitYawDegrees;
            EnterPoints = Copy(enterPoints, nameof(enterPoints));
            ExitPoints = Copy(exitPoints, nameof(exitPoints));
        }

        public Vector3 Sit { get; }

        public float SitYawDegrees { get; }

        public IReadOnlyList<Vector3> EnterPoints { get; }

        public IReadOnlyList<Vector3> ExitPoints { get; }

        internal static IReadOnlyList<Vector3> Copy(IReadOnlyList<Vector3> points, string paramName)
        {
            if (points == null)
            {
                return Array.Empty<Vector3>();
            }

            var copy = new Vector3[points.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = RequireFinite(points[i], paramName);
            }

            return copy;
        }

        internal static Vector3 RequireFinite(Vector3 value, string paramName)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            {
                throw new ArgumentException(paramName + " must be finite", paramName);
            }

            return value;
        }
    }
}
