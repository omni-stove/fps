using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// ワールドの位置と向きを、自分を中心にしたミニマップの位置とコンパスの方位へ直す。
    /// 向きは <see cref="MinimapContact"/> と同じく、上から見て北 (+Z) を 0 度に時計回り (東 +X が 90 度)。
    /// 地図の位置は中心が 0、+X が右、+Y が上で、単位は <see cref="MinimapSpec.Scale"/> を掛けたもの。高さ (Y) は見ない。
    /// </summary>
    public static class MinimapProjection
    {
        // 真上や真下に近い向きは水平の成分がわずかな揺れで大きく回るので、方位を出さずに無いと返す。
        private const float _minHorizontalLengthSquared = 1e-8f;

        private const float _degreesPerRadian = 180f / MathF.PI;

        /// <summary>
        /// worldPosition を地図の位置へ直す。spec が自分の向きを上にするなら selfHeadingDegrees の分だけ回す。
        /// <see cref="MinimapSpec.Radius"/> より遠いものは、同じ向きのまま縁 (<see cref="MinimapSpec.MapRadius"/>) に寄せ、onEdge を true にする。
        /// </summary>
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">位置か向きが有限でない。</exception>
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

        /// <summary>ToMap の status 版。自分の位置と向きは status から取る。</summary>
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public static Vector2 ToMap(MinimapSpec spec, MinimapStatus status, MinimapContact contact, out bool onEdge)
        {
            return ToMap(spec, status.SelfPosition, status.SelfHeadingDegrees, contact.Position, out onEdge);
        }

        /// <summary>地図の上で印を何度回して描くか (上が 0 で時計回り、0 以上 360 未満)。北が上なら headingDegrees のまま。</summary>
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">向きが有限でない。</exception>
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

        /// <summary>
        /// 向き (前を指すベクトル) から方位の角度 (0 以上 360 未満) を出す。
        /// 真上か真下を向いていて水平の向きが無いときは false を返し、degrees は 0。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">direction が有限でない。</exception>
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

        /// <summary>from から to への方位の角度。to が from の真上か真下なら false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">位置が有限でない。</exception>
        public static bool TryBearingDegrees(Vector3 from, Vector3 to, out float degrees)
        {
            MinimapContact.RequireFinite(from, nameof(from));
            MinimapContact.RequireFinite(to, nameof(to));
            return TryHeadingDegrees(to - from, out degrees);
        }

        /// <summary>
        /// 自分の向きから見て bearingDegrees が何度ずれているか。右が正で、-180 以上 180 未満。
        /// コンパスの帯で、方位や印を中心からどれだけ横に置くかに使う。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">角度が有限でない。</exception>
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
