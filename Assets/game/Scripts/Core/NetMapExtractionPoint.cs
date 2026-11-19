using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは ExtractionZone を読めないので、Editor がシーンの脱出地点から書き出した Id・中心・半径を持つ。
    public sealed class NetMapExtractionPoint
    {
        public NetMapExtractionPoint(string id, Vector3 center, float radius)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!NetMapLootContainer.IsFinite(center))
            {
                throw new ArgumentOutOfRangeException(nameof(center), center, "center must be finite");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            Id = id;
            Center = center;
            Radius = radius;
        }

        public string Id { get; }

        public Vector3 Center { get; }

        public float Radius { get; }

        // 高さを見ないのは、オフラインの ExtractionZone と同じ判定にするため
        public bool Contains(Vector3 position)
        {
            var dx = position.X - Center.X;
            var dz = position.Z - Center.Z;
            return (dx * dx) + (dz * dz) <= Radius * Radius;
        }
    }
}
