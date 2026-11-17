using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class ShotValidator
    {
        // 目の高さと、予測で先に進んでいる分 (ダッシュ 8m/s で 0.2 秒ほど) を見込んだ長さ。
        public const float DefaultMaxOriginOffset = 3f;

        // これより短い向きは 0 とみなす。
        private const float _minDirectionLengthSquared = 1e-12f;

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
