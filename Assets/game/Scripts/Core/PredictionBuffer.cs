using System;

namespace Fps.Core
{
    /// <summary>
    /// クライアント予測の履歴。tick ごとに入力とそれを適用した後の状態をリングバッファに持つ。
    /// サーバーから tick の確定状態が届いたら予測と比べ、ずれが許容範囲を超えていればその tick に巻き戻して
    /// 以降の入力を再シミュレーションする。容量を超えたら古い記録から捨てる。
    /// 「tick の状態」は、その tick の入力を適用した後の状態を指す。
    /// </summary>
    /// <typeparam name="TInput">1 tick 分の入力。</typeparam>
    /// <typeparam name="TState">シミュレーションの状態。</typeparam>
    public sealed class PredictionBuffer<TInput, TState>
    {
        /// <summary>既定の容量。60Hz で約 2 秒分。</summary>
        public const int DefaultCapacity = 128;

        private readonly Func<TState, TInput, TState> _simulate;
        private readonly Func<TState, TState, float> _measureError;
        private readonly int[] _ticks;
        private readonly TInput[] _inputs;
        private readonly TState[] _states;
        private int _start;

        /// <summary>ずれがこの値を超えたら照合する。</summary>
        public float Tolerance { get; }

        /// <summary>保持できる記録の数。</summary>
        public int Capacity => _ticks.Length;

        /// <summary>今保持している (まだ確定していない) 記録の数。</summary>
        public int Count { get; private set; }

        /// <summary>最新の状態の tick。</summary>
        public int CurrentTick { get; private set; }

        /// <summary>最新の状態。</summary>
        public TState Current { get; private set; }

        /// <summary>
        /// 最後に確定状態を受け取った tick。それ以前の確定状態は古いものとして無視する。
        /// <see cref="Reset"/> 直後はその tick。
        /// </summary>
        public int LastConfirmedTick { get; private set; }

        /// <param name="simulate">状態に 1 tick 分の入力を適用した次の状態を返す。同じ引数なら同じ結果を返すこと。</param>
        /// <param name="measureError">2 つの状態のずれ (0 以上)。</param>
        /// <param name="tolerance">ずれの許容値。0 以上の有限値。</param>
        /// <param name="capacity">保持する記録の数。1 以上。</param>
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

        /// <summary>記録をすべて捨て、tick の状態を起点にする。生成直後やテレポート時に呼ぶ。</summary>
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

        /// <summary>
        /// 最新の状態に入力を適用して tick の状態を予測し、入力と結果を記録する。
        /// </summary>
        /// <param name="tick"><see cref="CurrentTick"/> より大きい tick。</param>
        /// <param name="input">この tick の入力。</param>
        /// <returns>予測した状態。</returns>
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

        /// <summary>
        /// tick の確定状態と予測を比べる。ずれが <see cref="Tolerance"/> を超えていれば確定状態に置き換え、
        /// 以降の入力を再シミュレーションして <see cref="Current"/> を更新する。
        /// どちらの場合も tick 以前の記録は確定したものとして捨てる。
        /// </summary>
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

        /// <summary>tick の記録の論理位置 (0 が最古) を返す。無ければ -1。</summary>
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

        /// <summary>古い方から count 件の記録を捨てる。</summary>
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
