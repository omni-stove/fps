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

        public EnemyPerception(Vector3 self, Vector3? target, bool hasLineOfSight, bool heardNoise, Vector3? noisePosition)
        {
            Self = self;
            Target = target;
            HasLineOfSight = hasLineOfSight;
            HeardNoise = heardNoise;
            NoisePosition = noisePosition;
        }
    }
}
