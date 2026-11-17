using System;

namespace Shooter.Core
{
    public sealed class KillCamPlayback
    {
        public KillCamPlayback(KillCamClip clip)
        {
            Clip = clip ?? KillCamClip.Empty;
            IsFinished = Clip.IsEmpty;
        }

        public KillCamClip Clip { get; }

        public float Elapsed { get; private set; }

        public bool IsFinished { get; private set; }

        public float Progress => Clip.IsEmpty ? 1f : Math.Min(1f, Elapsed / Clip.Duration);

        public KillCamPose Pose => Clip.Sample(Elapsed);

        public bool Advance(float deltaSeconds, float speed)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!IsValidSpeed(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be positive and finite");
            }

            if (IsFinished)
            {
                return false;
            }

            Elapsed = Math.Min(Clip.Duration, Elapsed + (deltaSeconds * speed));
            if (Elapsed >= Clip.Duration)
            {
                IsFinished = true;
                return true;
            }

            return false;
        }

        public static bool IsValidSpeed(float speed)
        {
            return speed > 0f && !float.IsInfinity(speed);
        }
    }
}
