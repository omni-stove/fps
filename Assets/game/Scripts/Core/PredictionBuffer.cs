using System;

namespace Shooter.Core
{
    public sealed class PredictionBuffer<TInput, TState>
    {
        // 60Hz で約 2 秒分を持てる大きさにしてある。
        public const int DefaultCapacity = 128;

        // 巻き戻して同じ入力を流し直すので、simulate は同じ引数なら同じ結果を返すものを渡す。
        private readonly Func<TState, TInput, TState> _simulate;
        private readonly Func<TState, TState, float> _measureError;
        private readonly int[] _ticks;
        private readonly TInput[] _inputs;
        private readonly TState[] _states;
        private int _start;

        public float Tolerance { get; }

        public int Capacity => _ticks.Length;

        public int Count { get; private set; }

        public int CurrentTick { get; private set; }

        public TState Current { get; private set; }

        public int LastConfirmedTick { get; private set; }

        public PredictionBuffer(
            Func<TState, TInput, TState> simulate,
            Func<TState, TState, float> measureError,
            float tolerance,
            int capacity = DefaultCapacity)
        {
            _simulate = simulate ?? throw new ArgumentNullException(nameof(simulate));
            _measureError = measureError ?? throw new ArgumentNullException(nameof(measureError));

            if (!(tolerance >= 0f) || float.IsInfinity(tolerance))
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "tolerance must be finite and not negative");
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be positive");
            }

            Tolerance = tolerance;
            _ticks = new int[capacity];
            _inputs = new TInput[capacity];
            _states = new TState[capacity];
        }

        public void Reset(int tick, TState state)
        {
            Array.Clear(_inputs, 0, _inputs.Length);
            Array.Clear(_states, 0, _states.Length);
            _start = 0;
            Count = 0;
            CurrentTick = tick;
            LastConfirmedTick = tick;
            Current = state;
        }

        public TState Predict(int tick, TInput input)
        {
            if (tick <= CurrentTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must be newer than CurrentTick");
            }

            var state = _simulate(Current, input);

            int index;
            if (Count < Capacity)
            {
                index = IndexOf(Count);
                Count++;
            }
            else
            {
                index = _start;
                _start = (_start + 1) % Capacity;
            }

            _ticks[index] = tick;
            _inputs[index] = input;
            _states[index] = state;
            CurrentTick = tick;
            Current = state;
            return state;
        }

        public bool TryGet(int tick, out TInput input, out TState state)
        {
            var logical = FindLogical(tick);
            if (logical < 0)
            {
                input = default;
                state = default;
                return false;
            }

            var index = IndexOf(logical);
            input = _inputs[index];
            state = _states[index];
            return true;
        }

        public ReconcileResult Reconcile(int tick, TState authoritative)
        {
            if (tick <= LastConfirmedTick)
            {
                return new ReconcileResult(ReconcileOutcome.Ignored, tick, 0f, 0);
            }

            var logical = FindLogical(tick);
            if (logical < 0)
            {
                return new ReconcileResult(ReconcileOutcome.Ignored, tick, 0f, 0);
            }

            var error = _measureError(_states[IndexOf(logical)], authoritative);
            if (float.IsNaN(error) || error < 0f)
            {
                throw new InvalidOperationException("measureError must return zero or a positive value");
            }

            // tick までの記録はもう巻き戻しに使わない
            Discard(logical + 1);
            LastConfirmedTick = tick;

            if (error <= Tolerance)
            {
                return new ReconcileResult(ReconcileOutcome.Matched, tick, error, 0);
            }

            var state = authoritative;
            for (var i = 0; i < Count; i++)
            {
                var index = IndexOf(i);
                state = _simulate(state, _inputs[index]);
                _states[index] = state;
            }

            Current = state;
            return new ReconcileResult(ReconcileOutcome.Corrected, tick, error, Count);
        }

        private int FindLogical(int tick)
        {
            if (Count == 0 || tick < _ticks[IndexOf(0)] || tick > _ticks[IndexOf(Count - 1)])
            {
                return -1;
            }

            // tick は記録順に増えるので二分探索できる
            var lo = 0;
            var hi = Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var midTick = _ticks[IndexOf(mid)];
                if (midTick == tick)
                {
                    return mid;
                }

                if (midTick < tick)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return -1;
        }

        private void Discard(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var index = IndexOf(i);
                _inputs[index] = default;
                _states[index] = default;
            }

            _start = IndexOf(count);
            Count -= count;
        }

        private int IndexOf(int logical)
        {
            return (_start + logical) % Capacity;
        }
    }
}
