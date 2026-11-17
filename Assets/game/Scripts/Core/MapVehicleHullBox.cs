using System;
using System.Numerics;

namespace Shooter.Core
{
    // 車体を箱 1 つにまとめないのは、屋根の無い車の座席のように開いた所から中の人に弾が届くようにするため (Unity の当たり判定と同じ形)。
    public sealed class MapVehicleHullBox
    {
        public MapVehicleHullBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, string partId = null)
        {
            Center = MapVehicleSeat.RequireFinite(center, nameof(center));
            if (!(halfExtents.X > 0f && halfExtents.Y > 0f && halfExtents.Z > 0f) || !float.IsFinite(halfExtents.X + halfExtents.Y + halfExtents.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), halfExtents, "halfExtents must be positive and finite");
            }

            HalfExtents = halfExtents;
            Rotation = MapVehicle.Normalize(rotation, nameof(rotation));
            PartId = string.IsNullOrWhiteSpace(partId) ? null : partId;
        }

        public Vector3 Center { get; }

        public Vector3 HalfExtents { get; }

        public Quaternion Rotation { get; }

        public string PartId { get; }
    }
}
