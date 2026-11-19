using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは LootContainer を読めないので、Editor がシーンの箱から書き出した Id・表・位置・届く距離を持つ。
    public sealed class NetMapLootContainer
    {
        public NetMapLootContainer(string id, string tableId, Vector3 position, float reach)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!(reach > 0f) || float.IsInfinity(reach))
            {
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "reach must be positive and finite");
            }

            Id = id;
            TableId = tableId ?? throw new ArgumentNullException(nameof(tableId));
            Position = position;
            Reach = reach;
        }

        public string Id { get; }

        // 空を断らないのは、オフラインの LootContainer が表の無い箱を空のまま開けるのに揃えるため
        public string TableId { get; }

        public Vector3 Position { get; }

        public float Reach { get; }

        public bool IsInReach(Vector3 position)
        {
            var dx = position.X - Position.X;
            var dz = position.Z - Position.Z;
            return (dx * dx) + (dz * dz) <= Reach * Reach;
        }

        internal static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}
