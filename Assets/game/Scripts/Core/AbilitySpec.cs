using System;

namespace Shooter.Core
{
    public sealed class AbilitySpec
    {
        public const int DefaultMaxCharges = 1;

        public const float DefaultRechargeSeconds = 10f;

        public const float DefaultCooldownSeconds = 1f;

        public const float DefaultWindupSeconds = 0f;

        public static AbilitySpec Default { get; } =
            new AbilitySpec(DefaultMaxCharges, DefaultRechargeSeconds, DefaultCooldownSeconds, DefaultWindupSeconds);

        public AbilitySpec(int maxCharges, float rechargeSeconds, float cooldownSeconds, float windupSeconds)
        {
            if (maxCharges < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCharges), maxCharges, "maxCharges must be at least 1");
            }

            RequireSeconds(rechargeSeconds, nameof(rechargeSeconds));
            RequireSeconds(cooldownSeconds, nameof(cooldownSeconds));
            RequireSeconds(windupSeconds, nameof(windupSeconds));

            MaxCharges = maxCharges;
            RechargeSeconds = rechargeSeconds;
            CooldownSeconds = cooldownSeconds;
            WindupSeconds = windupSeconds;
        }

        public int MaxCharges { get; }

        public float RechargeSeconds { get; }

        public float CooldownSeconds { get; }

        public float WindupSeconds { get; }

        public bool Recharges => RechargeSeconds > 0f;

        private static void RequireSeconds(float seconds, string paramName)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(paramName, seconds, paramName + " must not be negative and must be finite");
            }
        }
    }
}
