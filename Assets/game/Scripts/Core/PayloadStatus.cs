using System;

namespace Shooter.Core
{
    // ModeStatus は出した後も UI が持つので、Payload を指さずに出した時点の値を写して持つ。
    public sealed class PayloadStatus
    {
        public PayloadStatus(
            Side attackers,
            float progress,
            int checkpointsReached,
            int checkpointCount,
            int pushers,
            bool isContested,
            bool isRollingBack,
            int minPushers = PayloadSpec.DefaultMinPushers)
        {
            if (minPushers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minPushers), minPushers, "minPushers must be at least 1");
            }

            if (!(progress >= 0f && progress <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be between 0 and 1");
            }

            if (checkpointCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), checkpointCount, "checkpointCount must not be negative");
            }

            if (checkpointsReached < 0 || checkpointsReached > checkpointCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpointsReached), checkpointsReached, "checkpointsReached must be between 0 and checkpointCount");
            }

            if (pushers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pushers), pushers, "pushers must not be negative");
            }

            Attackers = attackers;
            Progress = progress;
            CheckpointsReached = checkpointsReached;
            CheckpointCount = checkpointCount;
            Pushers = pushers;
            IsContested = isContested;
            IsRollingBack = isRollingBack;
            MinPushers = minPushers;
        }

        public int MinPushers { get; }

        public Side Attackers { get; }

        public float Progress { get; }

        public int CheckpointsReached { get; }

        public int CheckpointCount { get; }

        public int Pushers { get; }

        public bool IsContested { get; }

        public bool IsRollingBack { get; }

        public static PayloadStatus Of(Payload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return new PayloadStatus(
                payload.Attackers,
                payload.Progress,
                payload.CheckpointsReached,
                payload.Route.CheckpointCount,
                payload.Pushers,
                payload.IsContested,
                payload.IsRollingBack,
                payload.Spec.MinPushers);
        }
    }
}
