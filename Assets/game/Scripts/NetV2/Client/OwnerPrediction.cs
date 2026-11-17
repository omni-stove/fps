using System;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// 自分のプレイヤーの予測。tick ごとの入力をサーバーと同じ Core の移動で先に動かし、送る入力を覚えておく。
    /// スナップショットが届いたら、サーバーが処理した最後の入力の tick (<see cref="OwnerSnapshot.LastProcessedInputTick"/>) の予測と
    /// その行の状態を比べ、ずれていればその状態から後の入力をやり直す (<see cref="PredictionBuffer{TInput, TState}"/>)。
    /// </summary>
    /// <remarks>
    /// 入力は落ちてよい届け方で送るので、サーバーが処理したと分かるまでの入力を、送るたびに新しい方から
    /// <see cref="InputMessage.MaxFrames"/> 個まで載せ直す。
    /// </remarks>
    public sealed class OwnerPrediction
    {
        /// <summary>Netcode のクライアント (<c>NetworkPlayerMotor.ReconcileTolerance</c>) と同じ許容値 (m)。</summary>
        public const float DefaultTolerance = 0.05f;

        private readonly CharacterMotorSimulation _simulation;
        private readonly PredictionBuffer<CharacterMotorInput, CharacterMotorState> _buffer;
        private readonly InputFrame[] _frames;
        private int _frameStart;
        private int _frameCount;

        public OwnerPrediction(
            CharacterMotorSimulation simulation,
            float tolerance = DefaultTolerance,
            int capacity = PredictionBuffer<CharacterMotorInput, CharacterMotorState>.DefaultCapacity)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _buffer = new PredictionBuffer<CharacterMotorInput, CharacterMotorState>(
                simulation.Step, CharacterMotorState.PositionError, tolerance, capacity);
            _frames = new InputFrame[capacity];
        }

        /// <summary><see cref="Start"/> されたか。最初の自分の行が届くまでは予測しない。</summary>
        public bool IsStarted { get; private set; }

        /// <summary>最後に予測した tick。</summary>
        public int CurrentTick => _buffer.CurrentTick;

        /// <summary>最後に予測した状態。</summary>
        public CharacterMotorState Current => _buffer.Current;

        /// <summary>サーバーが処理したと分かっている最後の入力の tick。</summary>
        public int LastProcessedTick { get; private set; } = -1;

        /// <summary>まだサーバーが処理したと分からない入力の数。</summary>
        public int PendingFrames => _frameCount;

        public CharacterMotorSimulation Simulation => _simulation;

        /// <summary>
        /// tick の状態を起点にする。次の予測は tick + 1 から。サーバーは入力の tick を -1 より後から受け付けるので、最初は -1 を渡す。
        /// </summary>
        public void Start(int tick, CharacterMotorState state)
        {
            if (tick < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must be -1 or later");
            }

            _buffer.Reset(tick, state);
            _simulation.Place(state);
            _frameStart = 0;
            _frameCount = 0;
            LastProcessedTick = tick;
            IsStarted = true;
        }

        /// <summary>
        /// frame を次の tick の入力として予測し、送るために覚える。input は frame をサーバーと同じに読み直したもの
        /// (<see cref="SnapshotMotion.ToInput"/>)。
        /// </summary>
        /// <exception cref="InvalidOperationException">まだ <see cref="Start"/> していない。</exception>
        /// <exception cref="ArgumentException">frame の tick が <see cref="CurrentTick"/> の次でない。</exception>
        public CharacterMotorState Predict(InputFrame frame, CharacterMotorInput input)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("prediction has not started");
            }

            if (frame.Tick != _buffer.CurrentTick + 1)
            {
                throw new ArgumentException(
                    "frame tick " + frame.Tick + " must follow the predicted tick " + _buffer.CurrentTick, nameof(frame));
            }

            var state = _buffer.Predict(frame.Tick, input);
            if (_frameCount == _frames.Length)
            {
                _frameStart = (_frameStart + 1) % _frames.Length;
                _frameCount--;
            }

            _frames[(_frameStart + _frameCount) % _frames.Length] = frame;
            _frameCount++;
            return state;
        }

        /// <summary>
        /// サーバーが lastProcessedInputTick までの入力を処理した後の状態が authoritative だった。予測と比べて、ずれていればやり直す。
        /// </summary>
        public ReconcileResult Reconcile(int lastProcessedInputTick, CharacterMotorState authoritative)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("prediction has not started");
            }

            if (lastProcessedInputTick > LastProcessedTick)
            {
                LastProcessedTick = lastProcessedInputTick;
                while (_frameCount > 0 && _frames[_frameStart].Tick <= lastProcessedInputTick)
                {
                    _frameStart = (_frameStart + 1) % _frames.Length;
                    _frameCount--;
                }
            }

            var result = _buffer.Reconcile(lastProcessedInputTick, authoritative);

            // やり直しは体を途中の状態に置いたまま終わるので、表示と次の予測のために最新の状態へ置き直す
            if (result.Outcome == ReconcileOutcome.Corrected)
            {
                _simulation.Place(_buffer.Current);
            }

            return result;
        }

        /// <summary>
        /// 乗り物の座席にいる間の照合。座席の位置は乗り物の動きで決まり入力からやり直せないので、比べずにサーバーの状態を今の予測にする。
        /// まだ処理されていない入力は送り直すために残す。
        /// </summary>
        public void Rebase(int lastProcessedInputTick, CharacterMotorState authoritative)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("prediction has not started");
            }

            if (lastProcessedInputTick > LastProcessedTick)
            {
                LastProcessedTick = lastProcessedInputTick;
                while (_frameCount > 0 && _frames[_frameStart].Tick <= lastProcessedInputTick)
                {
                    _frameStart = (_frameStart + 1) % _frames.Length;
                    _frameCount--;
                }
            }

            _buffer.Reset(_buffer.CurrentTick, authoritative);
            _simulation.Place(authoritative);
        }

        /// <summary>
        /// サーバーがまだ処理していない入力を、新しい方から最大 <see cref="InputMessage.MaxFrames"/> 個、古い順に message へ入れ直す。
        /// </summary>
        public void FillInput(InputMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.Clear();
            var count = Math.Min(_frameCount, InputMessage.MaxFrames);
            for (var i = _frameCount - count; i < _frameCount; i++)
            {
                message.Add(_frames[(_frameStart + i) % _frames.Length]);
            }
        }
    }
}
