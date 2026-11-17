using System;

namespace Shooter.Core
{
    public sealed class CoopHold
    {
        public CoopHold(CoopHoldSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public CoopHoldSpec Spec { get; }

        public int HoldingCount { get; private set; }

        public float ElapsedSeconds { get; private set; }

        public bool IsRunning { get; private set; }

        public bool IsCompleted { get; private set; }

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

        public void Reset()
        {
            HoldingCount = 0;
            ElapsedSeconds = 0f;
            IsRunning = false;
            IsCompleted = false;
        }
    }
}
