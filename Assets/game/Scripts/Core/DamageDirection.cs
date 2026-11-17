using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class DamageDirection
    {
        public const float DefaultSeconds = 1f;

        // これより短い水平の距離は、向きが決まらないとみなす (自分の位置から撃たれた・真上や真下から撃たれた)。
        private const float _minLengthSquared = 1e-8f;

        // 高さの差を無視して水平面の向きだけを使うのは、上下を HUD の円周の上では表せないため。
        public static bool TryScreenAngle(Vector3 viewer, float viewerYaw, Vector3 source, out float degrees)
        {
            degrees = 0f;
            if (!IsFinite(viewerYaw))
            {
                return false;
            }

            var dx = source.X - viewer.X;
            var dz = source.Z - viewer.Z;
            var lengthSquared = (dx * dx) + (dz * dz);
            if (!IsFinite(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                return false;
            }

            var sourceYaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            degrees = Normalize(sourceYaw - viewerYaw);
            return true;
        }

        public static float Opacity(float elapsed, float duration)
        {
            if (!IsFinite(elapsed) || !IsFinite(duration) || duration <= 0f)
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(1f, 1f - (elapsed / duration)));
        }

        private static float Normalize(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            // -1e-6 % 360 + 360 は float で 360 に丸まることがあるので、範囲の上端を閉じておく。
            return wrapped >= 360f ? 0f : wrapped;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
