using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct EnemyPerception
    {
        public Vector3 Self { get; }

        public Vector3? Target { get; }

        public bool HasLineOfSight { get; }

        public bool HeardNoise { get; }

        public Vector3? NoisePosition { get; }

        public bool WasHit { get; }

        public Vector3? HitSource { get; }

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

        public EnemyPerception WithSuppression(float level, bool isSuppressed)
        {
            return new EnemyPerception(
                Self, Target, HasLineOfSight, HeardNoise, NoisePosition, WasHit, HitSource, HealthFraction, Awareness, isSuppressed, level);
        }
    }
}
