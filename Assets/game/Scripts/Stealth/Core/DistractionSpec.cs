using System;

namespace Shooter.Core
{
    public sealed class DistractionSpec
    {
        // グレネードより軽い物なので、同じ力で投げると少し遠くまで届く。
        public const float DefaultThrowSpeed = 16f;
        public const float DefaultThrowLiftSpeed = GrenadeSpec.DefaultThrowLiftSpeed;

        // 見張り 1 人の背後に落として、離れた見張りまで呼ばないくらいの広さ。
        public const float DefaultNoiseRadius = 15f;

        // 穴や場外へ落ちて何にも当たらなかった物を、いつまでも残さない。
        public const float DefaultMaxFlightSeconds = 6f;

        public const float DefaultLingerSeconds = 4f;

        public static readonly DistractionSpec Default = new DistractionSpec();

        public DistractionSpec(
            float throwSpeed = DefaultThrowSpeed,
            float throwLiftSpeed = DefaultThrowLiftSpeed,
            float noiseRadius = DefaultNoiseRadius,
            float maxFlightSeconds = DefaultMaxFlightSeconds,
            float lingerSeconds = DefaultLingerSeconds)
        {
            RequirePositiveFinite(throwSpeed, nameof(throwSpeed));
            RequireNonNegativeFinite(throwLiftSpeed, nameof(throwLiftSpeed));
            RequirePositiveFinite(noiseRadius, nameof(noiseRadius));
            RequirePositiveFinite(maxFlightSeconds, nameof(maxFlightSeconds));
            RequireNonNegativeFinite(lingerSeconds, nameof(lingerSeconds));

            ThrowSpeed = throwSpeed;
            ThrowLiftSpeed = throwLiftSpeed;
            NoiseRadius = noiseRadius;
            MaxFlightSeconds = maxFlightSeconds;
            LingerSeconds = lingerSeconds;
        }

        public float ThrowSpeed { get; }
        public float ThrowLiftSpeed { get; }
        public float NoiseRadius { get; }
        public float MaxFlightSeconds { get; }
        public float LingerSeconds { get; }

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
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative and must be finite");
            }
        }
    }
}
