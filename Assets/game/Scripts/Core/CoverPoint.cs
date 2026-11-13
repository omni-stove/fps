using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 敵が隠れに行く遮蔽点 1 つ (<see cref="CoverPointSelector"/> に渡す)。座標は Y が上、長さは m。
    /// </summary>
    public readonly struct CoverPoint
    {
        private const float _minFacingLength = 1e-6f;

        /// <param name="position">隠れるときに立つ足元の位置。</param>
        /// <param name="facing">この点から壁へ向かう方向。この向きから来る弾を壁が遮る。上下の成分は捨て、長さ 1 にして持つ。</param>
        /// <param name="height">壁が低い (しゃがんで隠れる) か高い (立って隠れる) か。</param>
        /// <exception cref="ArgumentOutOfRangeException">position・facing が有限でないとき、facing に水平の成分が無いとき。</exception>
        public CoverPoint(Vector3 position, Vector3 facing, CoverHeight height)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!IsFinite(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must be finite");
            }

            var flat = new Vector3(facing.X, 0f, facing.Z);
            var length = flat.Length();
            if (!(length > _minFacingLength))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must have a horizontal component");
            }

            Position = position;
            Facing = flat / length;
            Height = height;
        }

        /// <summary>隠れるときに立つ足元の位置。</summary>
        public Vector3 Position { get; }

        /// <summary>この点から壁へ向かう水平の単位ベクトル。</summary>
        public Vector3 Facing { get; }

        /// <summary>壁の高さの区分。</summary>
        public CoverHeight Height { get; }

        internal static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
