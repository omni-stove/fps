using System;

namespace Fps.Core
{
    // 線ごとに変えられるよう、ルール (ActionRules) ではなく Zipline コンポーネントの Inspector の値から作る。
    public sealed class ZiplineSpec
    {
        public const float DefaultMinSpeed = 4f;
        public const float DefaultMaxSpeed = 14f;
        public const float DefaultGravity = 9.81f;
        public const float DefaultEndMargin = 0.5f;

        public static readonly ZiplineSpec Default = new ZiplineSpec(DefaultMinSpeed, DefaultMaxSpeed, DefaultGravity, DefaultEndMargin);

        public ZiplineSpec(float minSpeed, float maxSpeed, float gravity, float endMargin)
        {
            // 0 を許すと高低差の無い線で止まったまま端に着かない。
            if (!(minSpeed > 0f) || float.IsInfinity(minSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(minSpeed), minSpeed, "minSpeed must be positive and finite");
            }

            if (!(maxSpeed >= minSpeed) || float.IsInfinity(maxSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed), maxSpeed, "maxSpeed must be finite and at least minSpeed");
            }

            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be finite and not negative");
            }

            if (!(endMargin >= 0f) || float.IsInfinity(endMargin))
            {
                throw new ArgumentOutOfRangeException(nameof(endMargin), endMargin, "endMargin must be finite and not negative");
            }

            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
            Gravity = gravity;
            EndMargin = endMargin;
        }

        public float MinSpeed { get; }

        public float MaxSpeed { get; }

        public float Gravity { get; }

        public float EndMargin { get; }
    }
}
