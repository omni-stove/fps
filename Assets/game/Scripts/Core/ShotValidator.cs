using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// クライアントから届いた射撃 (始点・向き・見ていた時点) をサーバーで受け付けてよいか調べる。
    /// 壁の向こうから撃つ・遠くから撃つといった、始点のごまかしを防ぐ。
    /// </summary>
    public static class ShotValidator
    {
        /// <summary>
        /// 始点とサーバー上の本人の位置のずれの既定の上限 (m)。目の高さと、予測で先に進んでいる分 (ダッシュ 8m/s で 0.2 秒ほど) を見込む。
        /// </summary>
        public const float DefaultMaxOriginOffset = 3f;

        // これより短い向きは 0 とみなす。
        private const float _minDirectionLengthSquared = 1e-12f;

        /// <summary>射撃を調べ、捨てるべき理由を返す。受け付けてよければ <see cref="ShotRejection.None"/>。</summary>
        /// <param name="origin">クライアントが送ってきた始点。</param>
        /// <param name="direction">クライアントが送ってきた向き。長さは問わない。</param>
        /// <param name="time">クライアントが見ていた時点。</param>
        /// <param name="shooterPosition">サーバーの知る撃った本人の今の位置。</param>
        /// <param name="maxOriginOffset">origin と shooterPosition の距離の上限 (m)。0 以上の有限値。</param>
        public static ShotRejection Check(Vector3 origin, Vector3 direction, ShotTime time, Vector3 shooterPosition, float maxOriginOffset)
        {
            if (float.IsNaN(maxOriginOffset) || float.IsInfinity(maxOriginOffset) || maxOriginOffset < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxOriginOffset), maxOriginOffset, "maxOriginOffset must be finite and not negative");
            }

            if (!IsFinite(origin))
            {
                return ShotRejection.InvalidOrigin;
            }

            if (!IsFinite(direction) || direction.LengthSquared() < _minDirectionLengthSquared)
            {
                return ShotRejection.InvalidDirection;
            }

            if (!time.IsValid)
            {
                return ShotRejection.InvalidTime;
            }

            // サーバー側の位置は自分で計算した値なので有限のはずだが、念のため比較が偽になる NaN も弾く
            var offset = Vector3.DistanceSquared(origin, shooterPosition);
            if (!(offset <= (double)maxOriginOffset * maxOriginOffset))
            {
                return ShotRejection.OriginTooFar;
            }

            return ShotRejection.None;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
