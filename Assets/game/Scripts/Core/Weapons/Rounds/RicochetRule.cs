using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class RicochetRule
    {
        public static bool TryBounce(
            RicochetSpec ricochet, int bounceCount, Vector3 direction, Vector3 normal, out Vector3 bounced, out float damageMultiplier)
        {
            if (bounceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bounceCount), bounceCount, "bounceCount must not be negative");
            }

            RequireDirection(direction, nameof(direction));
            RequireDirection(normal, nameof(normal));

            bounced = direction;
            damageMultiplier = 1f;
            if (ricochet == null || bounceCount >= ricochet.MaxBounces)
            {
                return false;
            }

            var d = Vector3.Normalize(direction);
            var n = Vector3.Normalize(normal);
            var into = -Vector3.Dot(d, n);

            // 法線と同じ側へ進む弾は面の裏から抜けていくところで、当たって折り返すものではない。
            if (!(into > 0f))
            {
                return false;
            }

            var grazingDegrees = MathF.Asin(MathF.Min(into, 1f)) * (180f / MathF.PI);
            if (grazingDegrees > ricochet.MaxGrazingAngleDegrees)
            {
                return false;
            }

            bounced = Vector3.Normalize(d + (2f * into * n));
            damageMultiplier = ricochet.DamageMultiplierPerBounce;
            return true;
        }

        private static void RequireDirection(Vector3 value, string paramName)
        {
            var lengthSquared = value.LengthSquared();
            if (!(lengthSquared > 1e-12f) || float.IsInfinity(lengthSquared))
            {
                throw new ArgumentException(paramName + " must be a non-zero finite vector", paramName);
            }
        }
    }
}
