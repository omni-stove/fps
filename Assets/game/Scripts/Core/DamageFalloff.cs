using System;

namespace Fps.Core
{
    /// <summary>
    /// 距離によるダメージ減衰の設定。不変。
    /// <see cref="StartDistance"/> 以下は倍率 1.0、<see cref="EndDistance"/> 以上は <see cref="MinMultiplier"/>、
    /// その間は線形補間する。
    /// </summary>
    public sealed class DamageFalloff
    {
        /// <summary>減衰しない設定（どの距離でも倍率 1.0）。</summary>
        public static DamageFalloff None { get; } = new DamageFalloff(float.MaxValue, float.MaxValue, 1f);

        /// <summary>減衰が始まる距離。この距離以下は倍率 1.0。</summary>
        public float StartDistance { get; }

        /// <summary>減衰が終わる距離。この距離以上は <see cref="MinMultiplier"/>。</summary>
        public float EndDistance { get; }

        /// <summary>最大まで減衰したときの倍率。0 以上 1 以下。</summary>
        public float MinMultiplier { get; }

        /// <param name="startDistance">減衰が始まる距離。0 以上の有限値。</param>
        /// <param name="endDistance">減衰が終わる距離。startDistance 以上の有限値。</param>
        /// <param name="minMultiplier">最大まで減衰したときの倍率。0 以上 1 以下。</param>
        public DamageFalloff(float startDistance, float endDistance, float minMultiplier)
        {
            if (!IsFinite(startDistance) || startDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(startDistance), startDistance, "startDistance must be finite and not negative");
            }

            if (!IsFinite(endDistance) || endDistance < startDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(endDistance), endDistance, "endDistance must be finite and not less than startDistance");
            }

            if (float.IsNaN(minMultiplier) || minMultiplier < 0f || minMultiplier > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(minMultiplier), minMultiplier, "minMultiplier must be between 0 and 1");
            }

            StartDistance = startDistance;
            EndDistance = endDistance;
            MinMultiplier = minMultiplier;
        }

        /// <summary>distance での減衰倍率（MinMultiplier 以上 1.0 以下）を返す。</summary>
        /// <param name="distance">射手から命中点までの距離。0 以上。正の無限大は EndDistance 以上として扱う。</param>
        public double MultiplierAt(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must not be negative");
            }

            if (distance <= StartDistance)
            {
                return 1.0;
            }

            if (distance >= EndDistance)
            {
                return MinMultiplierAsDouble();
            }

            var t = ((double)distance - StartDistance) / ((double)EndDistance - StartDistance);
            return 1.0 + ((MinMultiplierAsDouble() - 1.0) * t);
        }

        // float を 10 進経由で広げ、0.7f が 0.699999988... にならないようにする。
        private double MinMultiplierAsDouble()
        {
            return (double)(decimal)MinMultiplier;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
