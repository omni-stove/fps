using System;

namespace Shooter.Core
{
    /// <summary>
    /// アシストの調整値。倒される前の <see cref="WindowSeconds"/> 秒のうちに、倒された相手へ合わせて <see cref="MinDamage"/> 以上を与えた人をアシストとする。
    /// </summary>
    public sealed class AssistSpec
    {
        public const float DefaultWindowSeconds = 10f;

        // 体力 100 の 1/5。かすった 1 発ではアシストにならず、撃ち合いに加わった程度で届く値にする。
        public const int DefaultMinDamage = 20;

        public static AssistSpec Default { get; } = new AssistSpec();

        public float WindowSeconds { get; }

        public int MinDamage { get; }

        /// <exception cref="ArgumentOutOfRangeException">windowSeconds が正の有限でない、または minDamage が 1 未満。</exception>
        public AssistSpec(float windowSeconds = DefaultWindowSeconds, int minDamage = DefaultMinDamage)
        {
            WindowSeconds = SpecGuard.PositiveFinite(windowSeconds, nameof(windowSeconds));
            MinDamage = SpecGuard.AtLeast(minDamage, 1, nameof(minDamage));
        }

        /// <summary>hitTime に与えたダメージを、now に倒されたときに数えるか。ちょうど <see cref="WindowSeconds"/> 秒前までを数える。</summary>
        public bool InWindow(double hitTime, double now)
        {
            return now - hitTime <= WindowSeconds;
        }
    }
}
