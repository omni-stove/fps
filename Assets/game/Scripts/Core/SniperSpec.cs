using System;

namespace Fps.Core
{
    public sealed class SniperSpec
    {
        public const float DefaultAimSeconds = 2.5f;
        public const float DefaultShotDamageMultiplier = 3f;
        public const float DefaultReaimDelaySeconds = 1f;
        public const float DefaultRelocateGiveUpSeconds = 8f;

        public static readonly SniperSpec Default = new SniperSpec(
            DefaultAimSeconds,
            DefaultShotDamageMultiplier,
            DefaultReaimDelaySeconds,
            DefaultRelocateGiveUpSeconds);

        public float AimSeconds { get; }

        // 専用の武器を用意せず、持たせた武器のダメージに掛ける。狙撃兵ごとに武器の定義を増やさずに済むように。
        public float ShotDamageMultiplier { get; }

        public float ReaimDelaySeconds { get; }

        // 位置を変える先へ届かないとき (経路が塞がれたなど)、撃たずに歩き続けて黙ってしまわないよう、諦めてその場から狙い直す。
        public float RelocateGiveUpSeconds { get; }

        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。</exception>
        public SniperSpec(float aimSeconds, float shotDamageMultiplier, float reaimDelaySeconds, float relocateGiveUpSeconds)
        {
            // 0 秒にすると光線を見せずに撃てて、見てから避けられる一発という前提が崩れる。
            if (!(aimSeconds > 0f) || float.IsInfinity(aimSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(aimSeconds), aimSeconds, "aimSeconds must be positive and finite");
            }

            if (!(shotDamageMultiplier > 0f) || float.IsInfinity(shotDamageMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shotDamageMultiplier), shotDamageMultiplier, "shotDamageMultiplier must be positive and finite");
            }

            if (!(reaimDelaySeconds >= 0f) || float.IsInfinity(reaimDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reaimDelaySeconds), reaimDelaySeconds, "reaimDelaySeconds must be non-negative and finite");
            }

            if (!(relocateGiveUpSeconds > 0f) || float.IsInfinity(relocateGiveUpSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(relocateGiveUpSeconds), relocateGiveUpSeconds, "relocateGiveUpSeconds must be positive and finite");
            }

            AimSeconds = aimSeconds;
            ShotDamageMultiplier = shotDamageMultiplier;
            ReaimDelaySeconds = reaimDelaySeconds;
            RelocateGiveUpSeconds = relocateGiveUpSeconds;
        }
    }
}
