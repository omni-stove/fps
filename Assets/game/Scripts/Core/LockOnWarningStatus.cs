using System;

namespace Shooter.Core
{
    public readonly struct LockOnWarningStatus : IEquatable<LockOnWarningStatus>
    {
        public static LockOnWarningStatus None => default;

        // 向きは自分の正面から時計回りの度で持ち、(-180, 180] に揃える。同じ向きが 2 通りの値になって変化の知らせが余計に出ないようにするため。
        public LockOnWarningStatus(LockOnWarningStage stage, float bearingDegrees)
        {
            if (!Enum.IsDefined(typeof(LockOnWarningStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage is not a defined LockOnWarningStage");
            }

            if (float.IsNaN(bearingDegrees) || float.IsInfinity(bearingDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(bearingDegrees), bearingDegrees, "bearingDegrees must be finite");
            }

            Stage = stage;
            BearingDegrees = stage == LockOnWarningStage.None ? 0f : NormalizeBearing(bearingDegrees);
        }

        public LockOnWarningStage Stage { get; }

        public float BearingDegrees { get; }

        public bool IsWarning => Stage != LockOnWarningStage.None;

        public static float NormalizeBearing(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped > 180f)
            {
                wrapped -= 360f;
            }
            else if (wrapped <= -180f)
            {
                wrapped += 360f;
            }

            return wrapped;
        }

        public bool Equals(LockOnWarningStatus other)
        {
            return Stage == other.Stage && BearingDegrees.Equals(other.BearingDegrees);
        }

        public override bool Equals(object obj)
        {
            return obj is LockOnWarningStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Stage, BearingDegrees);
        }

        public static bool operator ==(LockOnWarningStatus left, LockOnWarningStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LockOnWarningStatus left, LockOnWarningStatus right)
        {
            return !left.Equals(right);
        }
    }
}
