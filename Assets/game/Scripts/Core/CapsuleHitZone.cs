using System;

namespace Shooter.Core
{
    public static class CapsuleHitZone
    {
        public const float HeadFraction = 0.85f;

        public const float LimbFraction = 0.4f;

        public static HitZone Classify(float hitHeight, float bottom, float height)
        {
            if (float.IsNaN(hitHeight) || float.IsInfinity(hitHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(hitHeight), hitHeight, "hitHeight must be finite");
            }

            if (float.IsNaN(bottom) || float.IsInfinity(bottom))
            {
                throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "bottom must be finite");
            }

            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive and finite");
            }

            var fraction = ((double)hitHeight - bottom) / height;
            if (fraction >= HeadFraction)
            {
                return HitZone.Head;
            }

            return fraction < LimbFraction ? HitZone.Limb : HitZone.Body;
        }
    }
}
