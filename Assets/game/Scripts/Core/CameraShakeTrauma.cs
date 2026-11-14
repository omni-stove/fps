using System;

namespace Fps.Core
{
    public static class CameraShakeTrauma
    {
        public static float ForExplosion(float distance, float atCenter, float radius)
        {
            RequireNonNegativeFinite(distance, nameof(distance));
            RequireNonNegativeFinite(atCenter, nameof(atCenter));
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a positive finite number");
            }

            if (distance >= radius)
            {
                return 0f;
            }

            return atCenter * (1f - (distance / radius));
        }

        public static float ForDamage(int amount, float perPoint, float max)
        {
            RequireNonNegativeFinite(perPoint, nameof(perPoint));
            RequireNonNegativeFinite(max, nameof(max));
            if (amount <= 0)
            {
                return 0f;
            }

            return Math.Min(max, amount * perPoint);
        }

        public static float ForLanding(float fallSpeed, float minSpeed, float fullSpeed, float max)
        {
            RequireNonNegativeFinite(fallSpeed, nameof(fallSpeed));
            RequireNonNegativeFinite(minSpeed, nameof(minSpeed));
            RequireNonNegativeFinite(max, nameof(max));
            if (!(fullSpeed > minSpeed) || float.IsInfinity(fullSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(fullSpeed), fullSpeed, "fullSpeed must be finite and greater than minSpeed");
            }

            if (fallSpeed <= minSpeed)
            {
                return 0f;
            }

            return max * Math.Min(1f, (fallSpeed - minSpeed) / (fullSpeed - minSpeed));
        }

        private static void RequireNonNegativeFinite(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "value must be a non-negative finite number");
            }
        }
    }
}
