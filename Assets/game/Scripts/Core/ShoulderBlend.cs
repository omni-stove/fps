using System;

namespace Shooter.Core
{
    public sealed class ShoulderBlend
    {
        public const float DefaultSeconds = 0.15f;

        public ShoulderBlend(ShoulderSide initial = ShoulderSide.Right, float seconds = DefaultSeconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a non-negative finite number");
            }

            Target = ValidateSide(initial, nameof(initial));
            Seconds = seconds;
            Progress = 1f;
        }

        public float Seconds { get; }

        public ShoulderSide Target { get; private set; }

        public float Progress { get; private set; }

        public bool IsBlending => Progress < 1f;

        // 動き始めと止まり際が急にならないよう、進み具合を smoothstep で緩める。
        public float OffsetSign
        {
            get
            {
                var to = Sign(Target);
                var t = Progress;
                var eased = t * t * (3f - (2f * t));
                return -to + (2f * to * eased);
            }
        }

        // smoothstep は s(1 - p) = 1 - s(p) なので、折り返すときは進み具合を 1 - p にすれば OffsetSign は飛ばない。
        public void Toggle()
        {
            Target = Target == ShoulderSide.Right ? ShoulderSide.Left : ShoulderSide.Right;
            Progress = 1f - Progress;
        }

        public bool SetTarget(ShoulderSide side)
        {
            if (ValidateSide(side, nameof(side)) == Target)
            {
                return false;
            }

            Toggle();
            return true;
        }

        public float Tick(float deltaSeconds)
        {
            ValidateDeltaSeconds(deltaSeconds);

            Progress = Seconds <= 0f ? 1f : Math.Min(1f, Progress + (deltaSeconds / Seconds));
            return OffsetSign;
        }

        // 状態を変える前に検査したい呼び出し元のため、Tick とは別に検査だけを置く。
        public static void ValidateDeltaSeconds(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }
        }

        private static float Sign(ShoulderSide side)
        {
            return side == ShoulderSide.Left ? -1f : 1f;
        }

        private static ShoulderSide ValidateSide(ShoulderSide side, string paramName)
        {
            if (side != ShoulderSide.Right && side != ShoulderSide.Left)
            {
                throw new ArgumentOutOfRangeException(paramName, side, "unknown shoulder side");
            }

            return side;
        }
    }
}
