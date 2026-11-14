using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 1 tick 分の敵の知覚。視線判定や音の検出は呼び出し側が行って渡す。
    /// </summary>
    public readonly struct EnemyPerception
    {
        /// <summary>敵自身の位置。</summary>
        public Vector3 Self { get; }

        /// <summary>目標の位置。目標がいなければ null。</summary>
        public Vector3? Target { get; }

        /// <summary>目標への視線が通っているか。</summary>
        public bool HasLineOfSight { get; }

        /// <summary>この tick に音を聞いたか。</summary>
        public bool HeardNoise { get; }

        /// <summary>聞いた音の位置。HeardNoise が false なら使われない。</summary>
        public Vector3? NoisePosition { get; }

        /// <summary>前の tick からこの tick までに撃たれたか。</summary>
        public bool WasHit { get; }

        /// <summary>撃った者の位置。分からなければ null。WasHit が false なら使われない。</summary>
        public Vector3? HitSource { get; }

        /// <summary>今の体力の割合。0 以上 1 以下。</summary>
        public float HealthFraction { get; }

        // 見えたら即座に気づく今の判断と、後で足す発見ゲージ (見えていても溜まるまで気づかない) の両方を表せるよう、視線とは別に持つ。
        public float Awareness { get; }

        public bool IsSuppressed { get; }

        // IsSuppressed は解けるまでの間 (度合いが下がっていく途中も) true のままなので、どれだけ撃ち込まれているかは別に持つ。
        public float SuppressionLevel { get; }

        public const float FullAwareness = 1f;

        public EnemyPerception(Vector3 self, Vector3? target, bool hasLineOfSight, bool heardNoise, Vector3? noisePosition)
            : this(self, target, hasLineOfSight, heardNoise, noisePosition, false, null, 1f)
        {
        }

        /// <exception cref="ArgumentOutOfRangeException">healthFraction が 0 以上 1 以下でないとき。</exception>
        public EnemyPerception(
            Vector3 self,
            Vector3? target,
            bool hasLineOfSight,
            bool heardNoise,
            Vector3? noisePosition,
            bool wasHit,
            Vector3? hitSource,
            float healthFraction)
            : this(
                self,
                target,
                hasLineOfSight,
                heardNoise,
                noisePosition,
                wasHit,
                hitSource,
                healthFraction,
                AwarenessFromSight(hasLineOfSight),
                false)
        {
        }

        /// <exception cref="ArgumentOutOfRangeException">healthFraction か awareness が 0 以上 1 以下でないとき。</exception>
        public EnemyPerception(
            Vector3 self,
            Vector3? target,
            bool hasLineOfSight,
            bool heardNoise,
            Vector3? noisePosition,
            bool wasHit,
            Vector3? hitSource,
            float healthFraction,
            float awareness,
            bool isSuppressed)
            // 度合いを渡さない呼び出し元は制圧を有無でしか知らないので、度合いは両端に寄せる。
            : this(
                self,
                target,
                hasLineOfSight,
                heardNoise,
                noisePosition,
                wasHit,
                hitSource,
                healthFraction,
                awareness,
                isSuppressed,
                isSuppressed ? 1f : 0f)
        {
        }

        /// <exception cref="ArgumentOutOfRangeException">healthFraction・awareness・suppressionLevel のどれかが 0 以上 1 以下でないとき。</exception>
        public EnemyPerception(
            Vector3 self,
            Vector3? target,
            bool hasLineOfSight,
            bool heardNoise,
            Vector3? noisePosition,
            bool wasHit,
            Vector3? hitSource,
            float healthFraction,
            float awareness,
            bool isSuppressed,
            float suppressionLevel)
        {
            if (!(healthFraction >= 0f) || healthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthFraction), healthFraction, "healthFraction must be between 0 and 1");
            }

            if (!(awareness >= 0f) || awareness > FullAwareness)
            {
                throw new ArgumentOutOfRangeException(nameof(awareness), awareness, "awareness must be between 0 and 1");
            }

            if (!(suppressionLevel >= 0f) || suppressionLevel > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(suppressionLevel), suppressionLevel, "suppressionLevel must be between 0 and 1");
            }

            Awareness = awareness;
            IsSuppressed = isSuppressed;
            SuppressionLevel = suppressionLevel;
            Self = self;
            Target = target;
            HasLineOfSight = hasLineOfSight;
            HeardNoise = heardNoise;
            NoisePosition = noisePosition;
            WasHit = wasHit;
            HitSource = hitSource;
            HealthFraction = healthFraction;
        }

        public static float AwarenessFromSight(bool hasLineOfSight)
        {
            return hasLineOfSight ? FullAwareness : 0f;
        }

        /// <exception cref="ArgumentOutOfRangeException">level が 0 以上 1 以下でないとき。</exception>
        public EnemyPerception WithSuppression(float level, bool isSuppressed)
        {
            return new EnemyPerception(
                Self, Target, HasLineOfSight, HeardNoise, NoisePosition, WasHit, HitSource, HealthFraction, Awareness, isSuppressed, level);
        }
    }
}
