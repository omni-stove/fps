using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 自分の予測は最初に自分の行が届いた時点の状態から始め、それまでは入力を送らない。サーバーは参加と同時に出現させるので、すぐ届く。
    public sealed class ClientMatch
    {
        private readonly MatchClient _client;
        private readonly OwnerPrediction _prediction;
        private readonly TickClock _clock;
        private readonly InputMessage _input = new InputMessage();
        private readonly Dictionary<int, RemoteTrack> _remotes = new Dictionary<int, RemoteTrack>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly HashSet<int> _concealed = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private int _nextTick;

        public ClientMatch(MatchClient client, CharacterMotorSimulation simulation)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (client.State != MatchClientState.Connected)
            {
                throw new ArgumentException("the client must be accepted first (state " + client.State + ")", nameof(client));
            }

            _prediction = new OwnerPrediction(simulation ?? throw new ArgumentNullException(nameof(simulation)));
            Vehicles = new ClientVehicles(client.PlayerId);
            _clock = new TickClock(client.TickRate);
            Interpolation = new InterpolationClock(client.TickRate);
            _client.SnapshotReceived += HandleSnapshot;
            if (_client.LatestSnapshot != null)
            {
                HandleSnapshot(_client.LatestSnapshot);
            }
        }

        public event Action<int> PlayerAdded;

        public event Action<int> PlayerRemoved;

        // オフラインの LadderMovement と同じく、つかまったら視点をはしごへ向けるため、はしごを向く yaw を渡す。
        public event Action<int, float> LadderGrabbed;

        public event Action<ReconcileResult> Reconciled;

        public int LocalPlayerId => _client.PlayerId;

        public OwnerPrediction Prediction => _prediction;

        public InterpolationClock Interpolation { get; }

        public ClientVehicles Vehicles { get; }

        public bool IsPredicting => _prediction.IsStarted;

        public CharacterMotorState LocalState => _prediction.Current;

        public IEnumerable<int> RemotePlayerIds => _remotes.Keys;

        public int Corrections { get; private set; }

        public float Alpha => _clock.Alpha;

        public int Advance(float deltaSeconds)
        {
            var latest = _client.LatestSnapshot;
            if (latest != null)
            {
                Interpolation.Advance(deltaSeconds, latest.Tick);
            }

            var ticks = _clock.Advance(deltaSeconds);
            return _prediction.IsStarted ? ticks : 0;
        }

        // サーバーと同じ答えにするため、ダウン中か・使えない動き・持ち物の倍率は、届いている自分の行と試合のルールから入れる。
        // pitch はグラップルの縄を撃つ向きにだけ使う。サーバーは真上・真下を超える値の入力を丸ごと捨てるので、送る前にその内へ収める
        public CharacterMotorState Step(Vector2 move, float yaw, InputButtons buttons, float pitch = 0f)
        {
            if (!_prediction.IsStarted)
            {
                throw new InvalidOperationException("prediction has not started; wait for IsPredicting");
            }

            if (!CharacterMotorInput.IsValid(move, yaw))
            {
                throw new ArgumentException("move and yaw must be finite");
            }

            if (float.IsNaN(pitch) || float.IsInfinity(pitch))
            {
                throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "pitch must be finite");
            }

            pitch = Math.Max(-InputFrame.MaxPitch, Math.Min(InputFrame.MaxPitch, pitch));

            // 座席にいる間の移動とボタンは乗り物の操作に使うので、歩く入力としてはサーバーに送らない (覗き込みだけは射撃のばらつきに効く)
            if (Vehicles.LocalSeat.IsRiding)
            {
                move = Vector2.Zero;
                buttons &= InputButtons.Aim;
            }

            var frame = new InputFrame(_nextTick, ClampMove(move), SnapshotMotion.WrapYaw(yaw), buttons, pitch);
            _nextTick++;
            var latest = _client.LatestSnapshot;
            var downed = false;
            var rules = default(MatchRulesSync);
            var carry = 1f;
            if (latest != null)
            {
                rules = latest.Match.Rules;
                carry = latest.HasOwner ? latest.Owner.CarrySpeedMultiplier : 1f;
                var own = latest.Players.IndexOf(_client.PlayerId);
                downed = own >= 0 && latest.Players[own].Downed.IsDowned;
            }

            var before = _prediction.Current.Ladder;
            var state = _prediction.Predict(frame, SnapshotMotion.ToInput(frame, downed, rules, carry));
            var ladder = state.Ladder;
            var ladders = _prediction.Simulation.Ladders;
            if (ladder.IsClimbing && (!before.IsClimbing || before.Index != ladder.Index) && ladder.Index < ladders.Count)
            {
                var facing = ladders[ladder.Index].Span.Facing;
                LadderGrabbed?.Invoke(ladder.Index, MathF.Atan2(facing.X, facing.Z));
            }

            return state;
        }

        public void SendInput()
        {
            if (!_prediction.IsStarted || _prediction.PendingFrames == 0)
            {
                return;
            }

            _prediction.FillInput(_input);
            _client.SendInput(_input);
        }

        public bool TrySampleRemote(int playerId, out RemotePose pose)
        {
            pose = default;
            return !_concealed.Contains(playerId)
                && _remotes.TryGetValue(playerId, out var track)
                && track.TrySample(Interpolation.RenderTick, out pose);
        }

        // 隠れた相手は名簿に残したまま (PlayerRemoved を出さず)、体だけを描かない
        public bool IsConcealed(int playerId)
        {
            return _concealed.Contains(playerId);
        }

        // 置き直された人が前の位置から滑って見えないよう、補間の記録を捨てる。
        public void ForgetRemoteHistory(int playerId)
        {
            if (_remotes.TryGetValue(playerId, out var track))
            {
                track.Clear();
            }
        }

        public void Detach()
        {
            _client.SnapshotReceived -= HandleSnapshot;
        }

        private static Vector2 ClampMove(Vector2 move)
        {
            var length = move.Length();
            return length > 1f ? move / length : move;
        }

        private void HandleSnapshot(SnapshotMessage snapshot)
        {
            Vehicles.Apply(snapshot);
            var players = snapshot.Players;
            _present.Clear();
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                _present.Add(row.PlayerId);
                if (row.PlayerId == _client.PlayerId)
                {
                    ReconcileOwn(snapshot, row);
                    continue;
                }

                var added = !_remotes.TryGetValue(row.PlayerId, out var track);
                if (added)
                {
                    track = new RemoteTrack();
                    _remotes.Add(row.PlayerId, track);
                }

                // 隠れた行の位置は空なので記録に入れない。記録を捨てておくのは、見え直したとき隠れる前の位置から滑って見えないよう、その tick から記録し直すため
                if (row.Concealed)
                {
                    _concealed.Add(row.PlayerId);
                    track.Clear();
                }
                else
                {
                    _concealed.Remove(row.PlayerId);
                    track.Record(snapshot.Tick, RemotePose.From(row));
                }

                if (added)
                {
                    PlayerAdded?.Invoke(row.PlayerId);
                }
            }

            _gone.Clear();
            foreach (var id in _remotes.Keys)
            {
                if (!_present.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                _remotes.Remove(id);
                _concealed.Remove(id);
                PlayerRemoved?.Invoke(id);
            }
        }

        private void ReconcileOwn(SnapshotMessage snapshot, in PlayerSnapshot row)
        {
            var processed = snapshot.HasOwner ? snapshot.Owner.LastProcessedInputTick : -1;
            var state = SnapshotMotion.ToState(row, snapshot.HasOwner ? snapshot.Owner.Knockback : CharacterKnockbackState.None);
            if (!_prediction.IsStarted)
            {
                _prediction.Start(processed, state);
                _nextTick = processed + 1;
                return;
            }

            if (row.Vehicle.IsRiding)
            {
                _prediction.Rebase(processed, state);
                return;
            }

            var result = _prediction.Reconcile(processed, state);
            if (result.Outcome == ReconcileOutcome.Corrected)
            {
                Corrections++;
            }

            if (result.Outcome != ReconcileOutcome.Ignored)
            {
                Reconciled?.Invoke(result);
            }
        }
    }
}
