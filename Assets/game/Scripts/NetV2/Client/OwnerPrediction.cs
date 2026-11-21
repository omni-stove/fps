using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 入力は落ちてよい届け方で送るので、サーバーが処理したと分かるまでの入力を、送るたびに新しい方から
    // InputMessage.MaxFrames 個まで載せ直す。
    public sealed class OwnerPrediction
    {
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

        public bool IsStarted { get; private set; }

        public int CurrentTick => _buffer.CurrentTick;

        public CharacterMotorState Current => _buffer.Current;

        public int LastProcessedTick { get; private set; } = -1;

        public int PendingFrames => _frameCount;

        public CharacterMotorSimulation Simulation => _simulation;

        // サーバーは入力の tick を -1 より後から受け付けるので、最初は -1 を渡す。
        public void Start(int tick, CharacterMotorState state)
        {
            if (tick < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must be -1 or later");
            }

            state = SnapshotMotion.Complete(state, default, _simulation.Water, _simulation.Body.Geometry);
            _buffer.Reset(tick, state);
            _simulation.Place(state);
            _frameStart = 0;
            _frameCount = 0;
            LastProcessedTick = tick;
            IsStarted = true;
        }

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

            var probed = false;
            if (_buffer.TryGet(lastProcessedInputTick, out var input, out var predicted))
            {
                authoritative = Complete(authoritative, predicted, input.Yaw, out probed);
            }

            var result = _buffer.Reconcile(lastProcessedInputTick, authoritative);

            // やり直しと壁の調べは体を途中の状態に置いたまま終わるので、表示と次の予測のために最新の状態へ置き直す
            if (result.Outcome == ReconcileOutcome.Corrected || probed)
            {
                _simulation.Place(_buffer.Current);
            }

            return result;
        }

        // 走り始めを予測し損ねた壁走りは、スナップショットに壁の向きが無いので、サーバーの位置でその tick の向きから壁を調べ直す。
        // 調べても同じ側に壁が無ければ法線を空のまま渡し、次の tick に壁を見失って走り終える。
        private CharacterMotorState Complete(CharacterMotorState authoritative, CharacterMotorState predicted, float yaw, out bool probed)
        {
            probed = false;
            var completed = SnapshotMotion.Complete(authoritative, predicted, _simulation.Water, _simulation.Body.Geometry);
            var wallRun = completed.WallRun;
            if (!wallRun.IsRunning || wallRun.Normal != Vector3.Zero)
            {
                return completed;
            }

            _simulation.Place(completed);
            probed = true;
            var wall = _simulation.WallSensor.Probe(completed.Position, yaw);
            return wall.Side == wallRun.Side
                ? SnapshotMotion.WithWallRun(completed, new CharacterWallRunState(wallRun.Side, _simulation.TickSeconds, wall.Normal))
                : completed;
        }

        // 座席の位置は乗り物の動きで決まり入力からやり直せないので、比べずにサーバーの状態を今の予測にする。
        // まだ処理されていない入力は送り直すために残す。
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
