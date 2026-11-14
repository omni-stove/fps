using System;

namespace Fps.Core
{
    /// <summary>
    /// 何人で何秒押し続けると済むか (<see cref="CoopHold"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class CoopHoldSpec
    {
        public const int DefaultRequiredCount = 2;

        public const float DefaultHoldSeconds = 3f;

        public const bool DefaultResetOnInterrupt = false;

        /// <summary>既定値 (<see cref="DefaultRequiredCount"/>・<see cref="DefaultHoldSeconds"/>・<see cref="DefaultResetOnInterrupt"/>) の設定。</summary>
        public static CoopHoldSpec Default { get; } = new CoopHoldSpec(DefaultRequiredCount, DefaultHoldSeconds, DefaultResetOnInterrupt);

        /// <param name="requiredCount">進めるのに同時に押している必要のある人数。1 以上。</param>
        /// <param name="holdSeconds">揃って押し続ける秒数。0 以上の有限値 (0 なら揃った時点で済む)。</param>
        /// <param name="resetOnInterrupt">人数が欠けたときに経過を 0 に戻すなら true。false なら経過を残して止める。</param>
        public CoopHoldSpec(int requiredCount, float holdSeconds, bool resetOnInterrupt = DefaultResetOnInterrupt)
        {
            if (requiredCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCount), requiredCount, "requiredCount must be at least 1");
            }

            if (!(holdSeconds >= 0f) || float.IsInfinity(holdSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "holdSeconds must not be negative and must be finite");
            }

            RequiredCount = requiredCount;
            HoldSeconds = holdSeconds;
            ResetOnInterrupt = resetOnInterrupt;
        }

        /// <summary>進めるのに同時に押している必要のある人数。1 以上。</summary>
        public int RequiredCount { get; }

        /// <summary>揃って押し続ける秒数。0 以上の有限値。</summary>
        public float HoldSeconds { get; }

        /// <summary>人数が欠けたときに経過を 0 に戻すなら true。false なら経過を残して止める。</summary>
        public bool ResetOnInterrupt { get; }
    }
}
