using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// 承認された後の試合の見え方。自分は入力を Core の移動で予測し (<see cref="OwnerPrediction"/>)、届いたスナップショットの
    /// 処理済みの入力の tick で照合する。他のプレイヤーは tick ごとの姿を残して少し遅らせて補間する (<see cref="RemoteTrack"/>)。
    /// スナップショットの並びに入った人・消えた人を <see cref="PlayerAdded"/>・<see cref="PlayerRemoved"/> で知らせる。
    /// </summary>
    /// <remarks>
    /// 自分の予測は、最初に自分の行が届いた時点の状態から始める。それまでは入力を送らない (サーバーは参加と同時に出現させるので、すぐ届く)。
    /// </remarks>
    public sealed class ClientMatch
    {
        private readonly MatchClient _client;
        private readonly OwnerPrediction _prediction;
        private readonly TickClock _clock;
        private readonly InputMessage _input = new InputMessage();
        private readonly Dictionary<int, RemoteTrack> _remotes = new Dictionary<int, RemoteTrack>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private int _nextTick;

        /// <param name="client">承認されたクライアント。</param>
        /// <param name="simulation">自分の体の移動。サーバーと同じマップ・同じ値で作ったもの。</param>
        public ClientMatch(MatchClient client, CharacterMotorSimulation simulation)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (client.State != MatchClientState.Connected)
            {
                throw new ArgumentException("the client must be accepted first (state " + client.State + ")", nameof(client));
            }

            _prediction = new OwnerPrediction(simulation ?? throw new ArgumentNullException(nameof(simulation)));
            _clock = new TickClock(client.TickRate);
            Interpolation = new InterpolationClock(client.TickRate);
            _client.SnapshotReceived += HandleSnapshot;
            if (_client.LatestSnapshot != null)
            {
                HandleSnapshot(_client.LatestSnapshot);
            }
        }

        /// <summary>スナップショットの並びに入った他のプレイヤー (Id)。</summary>
        public event Action<int> PlayerAdded;

        /// <summary>スナップショットの並びから消えた他のプレイヤー (Id)。</summary>
        public event Action<int> PlayerRemoved;

        /// <summary>
        /// 自分の予測がはしごにつかまった。渡すのははしごの番号と、はしごを向く yaw (ラジアン)。
        /// オフラインの LadderMovement と同じく、つかまったら視点をはしごへ向けるため。
        /// </summary>
        public event Action<int, float> LadderGrabbed;

        /// <summary>予測を照合した。巻き戻したかどうかは <see cref="ReconcileResult.Outcome"/>。</summary>
        public event Action<ReconcileResult> Reconciled;

        public int LocalPlayerId => _client.PlayerId;

        public OwnerPrediction Prediction => _prediction;

        public InterpolationClock Interpolation { get; }

        /// <summary>予測を始めたか (自分の行が届いたか)。</summary>
        public bool IsPredicting => _prediction.IsStarted;

        /// <summary>自分の最新の予測の状態。</summary>
        public CharacterMotorState LocalState => _prediction.Current;

        /// <summary>今表示している他のプレイヤーの Id。</summary>
        public IEnumerable<int> RemotePlayerIds => _remotes.Keys;

        /// <summary>巻き戻した回数。</summary>
        public int Corrections { get; private set; }

        /// <summary>今の tick とその次の tick の間のどこを表示しているか (0〜1)。射撃の要求に載せる。</summary>
        public float Alpha => _clock.Alpha;

        /// <summary>
        /// deltaSeconds だけ時間を進め、進んだ tick の数を返す。呼ぶ側はその数だけ <see cref="Step"/> を呼んでから <see cref="SendInput"/> を呼ぶ。
        /// 予測を始める前は 0 を返し、補間だけ進める。
        /// </summary>
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

        /// <summary>
        /// 次の tick の入力を予測に通して覚える。移動は X が右・Y が前、yaw は Y 軸まわりのラジアン。
        /// ダウン中か・使えない動きは、届いている自分の行と試合のルールから入れる (サーバーと同じ答えにするため)。
        /// </summary>
        public CharacterMotorState Step(Vector2 move, float yaw, InputButtons buttons)
        {
            if (!_prediction.IsStarted)
            {
                throw new InvalidOperationException("prediction has not started; wait for IsPredicting");
            }

            if (!CharacterMotorInput.IsValid(move, yaw))
            {
                throw new ArgumentException("move and yaw must be finite");
            }

            var frame = new InputFrame(_nextTick, ClampMove(move), SnapshotMotion.WrapYaw(yaw), buttons);
            _nextTick++;
            var latest = _client.LatestSnapshot;
            var downed = false;
            var rules = default(MatchRulesSync);
            if (latest != null)
            {
                rules = latest.Match.Rules;
                var own = latest.Players.IndexOf(_client.PlayerId);
                downed = own >= 0 && latest.Players[own].Downed.IsDowned;
            }

            var before = _prediction.Current.Ladder;
            var state = _prediction.Predict(frame, SnapshotMotion.ToInput(frame, downed, rules));
            var ladder = state.Ladder;
            var ladders = _prediction.Simulation.Ladders;
            if (ladder.IsClimbing && (!before.IsClimbing || before.Index != ladder.Index) && ladder.Index < ladders.Count)
            {
                var facing = ladders[ladder.Index].Span.Facing;
                LadderGrabbed?.Invoke(ladder.Index, MathF.Atan2(facing.X, facing.Z));
            }

            return state;
        }

        /// <summary>まだサーバーが処理していない入力をまとめて送る。</summary>
        public void SendInput()
        {
            if (!_prediction.IsStarted || _prediction.PendingFrames == 0)
            {
                return;
            }

            _prediction.FillInput(_input);
            _client.SendInput(_input);
        }

        /// <summary>他のプレイヤーの今表示する姿。記録が無ければ false。</summary>
        public bool TrySampleRemote(int playerId, out RemotePose pose)
        {
            pose = default;
            return _remotes.TryGetValue(playerId, out var track) && track.TrySample(Interpolation.RenderTick, out pose);
        }

        /// <summary>サーバーが他のプレイヤーを置き直した。前の位置から滑って見えないよう、補間の記録を捨てる。</summary>
        public void ForgetRemoteHistory(int playerId)
        {
            if (_remotes.TryGetValue(playerId, out var track))
            {
                track.Clear();
            }
        }

        /// <summary>購読を外す。</summary>
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

                if (!_remotes.TryGetValue(row.PlayerId, out var track))
                {
                    track = new RemoteTrack();
                    _remotes.Add(row.PlayerId, track);
                    track.Record(snapshot.Tick, RemotePose.From(row));
                    PlayerAdded?.Invoke(row.PlayerId);
                    continue;
                }

                track.Record(snapshot.Tick, RemotePose.From(row));
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
                PlayerRemoved?.Invoke(id);
            }
        }

        private void ReconcileOwn(SnapshotMessage snapshot, in PlayerSnapshot row)
        {
            var processed = snapshot.HasOwner ? snapshot.Owner.LastProcessedInputTick : -1;
            var state = SnapshotMotion.ToState(row);
            if (!_prediction.IsStarted)
            {
                _prediction.Start(processed, state);
                _nextTick = processed + 1;
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
