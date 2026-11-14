using System;

namespace Fps.Core
{
    /// <summary>
    /// 何人かで同時に押し続けると済む操作の進み。押している人数が <see cref="CoopHoldSpec.RequiredCount"/> 以上の間だけ
    /// 経過が進み、<see cref="CoopHoldSpec.HoldSeconds"/> に届いたら済む。誰が押しているかは呼ぶ側が数える。
    /// </summary>
    public sealed class CoopHold
    {
        public CoopHold(CoopHoldSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public CoopHoldSpec Spec { get; }

        /// <summary>直前の <see cref="Tick"/> で押していた人数。</summary>
        public int HoldingCount { get; private set; }

        /// <summary>揃って押し続けた秒数。<see cref="CoopHoldSpec.HoldSeconds"/> を超えない。</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>人数が揃って進んでいる間は true。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>済んだら true。<see cref="Reset"/> まで変わらない。</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>進み具合 (0 から 1)。</summary>
        public float Progress
        {
            get
            {
                if (IsCompleted)
                {
                    return 1f;
                }

                return Spec.HoldSeconds > 0f ? ElapsedSeconds / Spec.HoldSeconds : 0f;
            }
        }

        /// <summary>
        /// 今押している人数で deltaSeconds だけ進める。済んだあとは人数だけ覚えて何もしない。
        /// </summary>
        /// <param name="holdingCount">今押している人数。0 以上。</param>
        /// <param name="deltaSeconds">前の呼び出しからの秒数。0 以上の有限値。</param>
        public CoopHoldStep Tick(int holdingCount, float deltaSeconds)
        {
            if (holdingCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(holdingCount), holdingCount, "holdingCount must not be negative");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            HoldingCount = holdingCount;
            if (IsCompleted)
            {
                return CoopHoldStep.None;
            }

            if (holdingCount < Spec.RequiredCount)
            {
                if (!IsRunning)
                {
                    return CoopHoldStep.None;
                }

                IsRunning = false;
                if (Spec.ResetOnInterrupt)
                {
                    ElapsedSeconds = 0f;
                }

                return CoopHoldStep.Interrupted;
            }

            var step = IsRunning ? CoopHoldStep.None : CoopHoldStep.Started;
            IsRunning = true;
            ElapsedSeconds = Math.Min(ElapsedSeconds + deltaSeconds, Spec.HoldSeconds);
            if (ElapsedSeconds >= Spec.HoldSeconds)
            {
                IsRunning = false;
                IsCompleted = true;
                return CoopHoldStep.Completed;
            }

            return step;
        }

        /// <summary>経過と完了を消して、もう一度使えるようにする。</summary>
        public void Reset()
        {
            HoldingCount = 0;
            ElapsedSeconds = 0f;
            IsRunning = false;
            IsCompleted = false;
        }
    }
}
