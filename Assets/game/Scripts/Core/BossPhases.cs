using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class BossPhases
    {
        private readonly BossPhase[] _phases;

        public BossPhases(IReadOnlyList<BossPhase> phases)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            if (phases.Count == 0)
            {
                throw new ArgumentException("a boss needs at least one phase", nameof(phases));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            _phases = new BossPhase[phases.Count];
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i] ?? throw new ArgumentException("phase " + i + " is null", nameof(phases));

                // 最初の段階は始めから入っているので、条件を書いても使われない。書き間違いに気付けるよう受け付けない。
                if (i == 0 && phase.Trigger != null)
                {
                    throw new ArgumentException("the first phase is entered from the start and must not have a trigger", nameof(phases));
                }

                if (i > 0 && phase.Trigger == null)
                {
                    throw new ArgumentException("phase " + i + " ('" + phase.Id + "') has no trigger", nameof(phases));
                }

                if (!ids.Add(phase.Id))
                {
                    throw new ArgumentException("phase id '" + phase.Id + "' is used twice", nameof(phases));
                }

                _phases[i] = phase;
            }
        }

        public IReadOnlyList<BossPhase> Phases => _phases;

        public int CurrentIndex { get; private set; }

        public BossPhase Current => _phases[CurrentIndex];

        public bool IsFinal => CurrentIndex == _phases.Length - 1;

        public float SecondsInPhase { get; private set; }

        // 1 発で閾値を 2 つ越えたら途中の段階を飛ばして一度に進め、変化は 1 回だけ返す。
        // 途中の段階の設定や弱点を一瞬だけ当てても、見た目や判断にはむだな切り替えにしかならないため。
        public BossPhaseChange? Update(float healthFraction, float deltaSeconds)
        {
            if (!(healthFraction >= 0f) || healthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthFraction), healthFraction, "healthFraction must be between 0 and 1");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            SecondsInPhase += deltaSeconds;

            var previous = CurrentIndex;
            while (!IsFinal)
            {
                var next = _phases[CurrentIndex + 1];
                if (!next.Trigger.IsMet(new BossPhaseContext(healthFraction, SecondsInPhase, CurrentIndex)))
                {
                    break;
                }

                CurrentIndex++;
                SecondsInPhase = 0f;
            }

            return CurrentIndex != previous ? new BossPhaseChange(previous, CurrentIndex, _phases[previous], Current, healthFraction) : (BossPhaseChange?)null;
        }

        // 条件を待たずに次へ進める。ボスの行動の区切り (仕掛けを壊された・演出が終わったなど) で外から進めるのに使う。
        public BossPhaseChange? Advance(float healthFraction)
        {
            if (!(healthFraction >= 0f) || healthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthFraction), healthFraction, "healthFraction must be between 0 and 1");
            }

            if (IsFinal)
            {
                return null;
            }

            var previous = CurrentIndex;
            CurrentIndex++;
            SecondsInPhase = 0f;
            return new BossPhaseChange(previous, CurrentIndex, _phases[previous], Current, healthFraction);
        }

        public BossPhaseChange? Reset(float healthFraction)
        {
            if (!(healthFraction >= 0f) || healthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthFraction), healthFraction, "healthFraction must be between 0 and 1");
            }

            var previous = CurrentIndex;
            CurrentIndex = 0;
            SecondsInPhase = 0f;
            return previous != 0 ? new BossPhaseChange(previous, 0, _phases[previous], Current, healthFraction) : (BossPhaseChange?)null;
        }

        public static float HealthFraction(int current, int max)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be positive");
            }

            if (current <= 0)
            {
                return 0f;
            }

            return current >= max ? 1f : (float)current / max;
        }
    }

    public readonly struct BossPhaseChange
    {
        public BossPhaseChange(int previousIndex, int index, BossPhase previous, BossPhase current, float healthFraction)
        {
            PreviousIndex = previousIndex;
            Index = index;
            Previous = previous ?? throw new ArgumentNullException(nameof(previous));
            Current = current ?? throw new ArgumentNullException(nameof(current));
            HealthFraction = healthFraction;
        }

        public int PreviousIndex { get; }

        public int Index { get; }

        public BossPhase Previous { get; }

        public BossPhase Current { get; }

        public float HealthFraction { get; }

        public bool IsReset => Index < PreviousIndex;
    }
}
