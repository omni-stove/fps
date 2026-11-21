using System;
using System.Numerics;

namespace Shooter.Core
{
    // 時間だけを数え、どこへ落とすかは PickPoint で別に選ぶ。落とす場所は次の円で決まり、円は Runtime の側が持つため。
    public sealed class SupplyDropSchedule
    {
        public const int NoStage = -1;

        // 待ちを float で引いていくので、足すと待ちちょうどになる刻み (1 秒を 0.9 + 0.1) でも誤差でわずかに残り、1 フレーム遅れる
        private const float _snap = 1e-6f;

        private float _untilNext;

        public SupplyDropSchedule(SupplyDropSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public SupplyDropSpec Spec { get; }

        public int StageIndex { get; private set; } = NoStage;

        public int Remaining { get; private set; }

        public int DroppedThisStage { get; private set; }

        public bool IsDropping => Remaining > 0;

        public float SecondsUntilNext => IsDropping ? Math.Max(0f, _untilNext) : 0f;

        // 前の段階で落とし切れなかった分は捨てる。次の円が決まった後に前の円の中へ落とすと、外に落ちることがあるため。
        public int BeginStage(int phaseIndex)
        {
            var stage = Spec.StageFor(phaseIndex);
            StageIndex = phaseIndex;
            DroppedThisStage = 0;
            Remaining = stage != null ? stage.Count : 0;
            _untilNext = stage != null ? stage.DelaySeconds : 0f;
            return Remaining;
        }

        public void Stop()
        {
            StageIndex = NoStage;
            Remaining = 0;
            DroppedThisStage = 0;
            _untilNext = 0f;
        }

        // 1 フレームが間隔より長くても数え落とさないよう、その間に来た分をまとめて返す。
        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!IsDropping)
            {
                return 0;
            }

            var interval = Spec.StageFor(StageIndex).IntervalSeconds;
            _untilNext -= deltaSeconds;
            var due = 0;
            while (Remaining > 0 && _untilNext <= _snap)
            {
                due++;
                Remaining--;
                DroppedThisStage++;
                _untilNext += interval;
            }

            return due;
        }

        // 円の中で一様になるよう、半径は乱数の平方根で選ぶ (そのままだと中心に寄る)。
        public static Vector2 PickPoint(Vector2 center, float radius, float fraction, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a non-negative finite number");
            }

            if (!(fraction >= 0f && fraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "fraction must be between 0 and 1");
            }

            var reach = radius * fraction;
            var distance = reach * (float)Math.Sqrt(random.NextDouble());
            var angle = random.NextDouble() * Math.PI * 2.0;
            return center + new Vector2((float)Math.Cos(angle) * distance, (float)Math.Sin(angle) * distance);
        }
    }
}
