using System;
using System.Numerics;

namespace Shooter.Server
{
    // Bot を集める目的の場所。範囲の中を巡らせるので、中心と水平の半径を持つ
    internal readonly struct ObjectiveRally
    {
        public ObjectiveRally(Vector3 center, float radius)
        {
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(center.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(center), center, "center must be finite");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            Center = center;
            Radius = radius;
        }

        public Vector3 Center { get; }

        public float Radius { get; }
    }
}
