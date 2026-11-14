using System;

namespace Fps.Core
{
    public sealed class ChargerSpec
    {
        public const float DefaultTriggerDistance = 7f;
        public const float DefaultWindupSeconds = 0.9f;
        public const float DefaultChargeSpeed = 11f;
        public const float DefaultChargeDistance = 11f;
        public const float DefaultHitReach = 1.1f;
        public const int DefaultImpactDamage = 35;
        public const float DefaultStunSeconds = 1.8f;
        public const float DefaultRecoverSeconds = 1f;

        public static readonly ChargerSpec Default = new ChargerSpec(
            DefaultTriggerDistance,
            DefaultWindupSeconds,
            DefaultChargeSpeed,
            DefaultChargeDistance,
            DefaultHitReach,
            DefaultImpactDamage,
            DefaultStunSeconds,
            DefaultRecoverSeconds);

        public float TriggerDistance { get; }

        public float WindupSeconds { get; }

        public float ChargeSpeed { get; }

        public float ChargeDistance { get; }

        public float HitReach { get; }

        public int ImpactDamage { get; }

        public float StunSeconds { get; }

        public float RecoverSeconds { get; }

        public float ChargeSeconds => ChargeDistance / ChargeSpeed;

        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。triggerDistance は chargeDistance 以下。</exception>
        public ChargerSpec(
            float triggerDistance,
            float windupSeconds,
            float chargeSpeed,
            float chargeDistance,
            float hitReach,
            int impactDamage,
            float stunSeconds,
            float recoverSeconds)
        {
            RequirePositiveFinite(chargeSpeed, nameof(chargeSpeed));
            RequirePositiveFinite(chargeDistance, nameof(chargeDistance));

            // 突進の届く距離より遠くから構えると、まっすぐ走っても相手の手前で必ず止まる。
            if (!(triggerDistance > 0f) || triggerDistance > chargeDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triggerDistance), triggerDistance, "triggerDistance must be positive and not exceed chargeDistance");
            }

            // 0 秒にすると構えを見せずに突っ込み、見てから避けるという前提が崩れる。
            RequirePositiveFinite(windupSeconds, nameof(windupSeconds));
            RequirePositiveFinite(hitReach, nameof(hitReach));
            if (impactDamage < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(impactDamage), impactDamage, "impactDamage must be at least 1");
            }

            RequireNonNegativeFinite(stunSeconds, nameof(stunSeconds));
            RequireNonNegativeFinite(recoverSeconds, nameof(recoverSeconds));

            TriggerDistance = triggerDistance;
            WindupSeconds = windupSeconds;
            ChargeSpeed = chargeSpeed;
            ChargeDistance = chargeDistance;
            HitReach = hitReach;
            ImpactDamage = impactDamage;
            StunSeconds = stunSeconds;
            RecoverSeconds = recoverSeconds;
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }
    }
}
