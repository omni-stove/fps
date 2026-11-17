using System;
using System.Numerics;

namespace Shooter.Core
{
    // カメラからでなく目から撃つのは、三人称のカメラだけが壁の角から出ている状態で、壁の向こうを撃てないようにするため。
    public static class AimSolver
    {
        private const float _minLengthSquared = 1e-12f;

        // カメラと目の間を飛ばすのは、三人称でカメラと体の間にある物を狙ってしまわないため。
        public static float RayStart(Vector3 cameraPosition, Vector3 cameraForward, Vector3 eye)
        {
            var forward = NormalizeOrThrow(cameraForward, nameof(cameraForward));
            return Math.Max(0f, Vector3.Dot(eye - cameraPosition, forward));
        }

        public static Vector3 DirectionFromEye(Vector3 eye, Vector3 aimPoint, Vector3 fallback)
        {
            var normalizedFallback = NormalizeOrThrow(fallback, nameof(fallback));
            var toAim = aimPoint - eye;
            var lengthSquared = toAim.LengthSquared();
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                return normalizedFallback;
            }

            return toAim / (float)Math.Sqrt(lengthSquared);
        }

        // margin を引くのは、狙う点のすぐ手前の命中は狙った物そのものなので、遮りとみなさないため。
        public static bool IsBlocked(Vector3 eye, Vector3 aimPoint, float hitDistance, float margin)
        {
            if (!IsNonNegativeFinite(hitDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(hitDistance), hitDistance, "hitDistance must be a non-negative finite number");
            }

            if (!IsNonNegativeFinite(margin))
            {
                throw new ArgumentOutOfRangeException(nameof(margin), margin, "margin must be a non-negative finite number");
            }

            var lineLength = Vector3.Distance(eye, aimPoint);
            return hitDistance < lineLength - margin;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }

        private static Vector3 NormalizeOrThrow(Vector3 value, string name)
        {
            var lengthSquared = value.LengthSquared();
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                throw new ArgumentException(name + " must be a finite non-zero vector", name);
            }

            return value / (float)Math.Sqrt(lengthSquared);
        }
    }
}
