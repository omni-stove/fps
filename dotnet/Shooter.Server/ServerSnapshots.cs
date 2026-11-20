using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerSnapshots : IServerModule
    {
        public const int HistoryLength = 32;

        private readonly List<ISnapshotContributor> _contributors;
        private MatchRulesSync _rules;
        private MatchObjective _objective;
        private readonly Dictionary<ConnectionId, ClientHistory> _clients = new Dictionary<ConnectionId, ClientHistory>();
        private readonly MatchRulesModule _sides;

        // 同じまとまりの人は同じ答えを受け取るので、1 tick の間は見えたかを覚えて当たりを引き直さない
        private readonly Dictionary<(bool TeamWide, int Id, int Target), bool> _seenThisTick =
            new Dictionary<(bool TeamWide, int Id, int Target), bool>();
        private readonly List<Vector3> _eyes = new List<Vector3>();
        private ServerContext _context;
        private ServerVisibility _visibility;
        private double _now;

        // 敵味方と生死は目的 (BR の分隊・FFA) で決まるので、隠すにはルールのモジュールが要る
        public ServerSnapshots(
            GameRules rules, IEnumerable<ISnapshotContributor> contributors, AntiCheatTuning antiCheat = null, MatchRulesModule sides = null)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(contributors);
            AntiCheat = antiCheat ?? AntiCheatTuning.Default;
            if (AntiCheat.Conceal.Enabled && sides == null)
            {
                throw new ArgumentException("concealing hidden enemies needs the rules module to tell allies and the living", nameof(sides));
            }

            _sides = sides;
            _rules = MatchRulesSync.From(rules);
            _objective = rules.Objective;
            _contributors = new List<ISnapshotContributor>(contributors);
            if (_contributors.Contains(null))
            {
                throw new ArgumentException("contributors must not contain null", nameof(contributors));
            }
        }

        public string Name => "snapshots";

        public AntiCheatTuning AntiCheat { get; }

        public int SentCount { get; private set; }

        public int SentReliableCount { get; private set; }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.Match.RulesChanged += HandleRulesChanged;
            if (AntiCheat.Conceal.Enabled)
            {
                _visibility = new ServerVisibility(_context.World, AntiCheat.Conceal);
                _context.Match.MapChanged += HandleMapChanged;
                _context.Match.PlayerLeft += HandlePlayerLeft;
            }
        }

        public void Tick(long tick, float deltaSeconds)
        {
            // 1 秒 60 tick なら 1 年以上かかる。そこまで続けば、試合を区切って始め直すほうが先に来る
            if (tick > int.MaxValue)
            {
                throw new InvalidOperationException("the server tick no longer fits a snapshot tick");
            }

            _now += deltaSeconds;
            _seenThisTick.Clear();
            var snapshotTick = (int)tick;
            foreach (var player in _context.Match.Players)
            {
                if (!_clients.TryGetValue(player.Connection, out var history))
                {
                    continue;
                }

                var snapshot = history.Next(snapshotTick);
                Fill(snapshot, snapshotTick, player);
                snapshot.Baseline = history.Baseline(snapshotTick);
                var channel = _context.Messenger.SendPreferringUnreliable(player.Connection, snapshot);
                snapshot.Baseline = null;
                SentCount++;
                if (channel == TransportChannel.Reliable)
                {
                    SentReliableCount++;
                }
            }
        }

        public void OnConnected(ConnectionId connection)
        {
            _clients[connection] = new ClientHistory();
        }

        public void OnDisconnected(ConnectionId connection)
        {
            _clients.Remove(connection);
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (message is InputMessage input && _clients.TryGetValue(connection, out var history))
            {
                history.Acknowledge(input.AckedSnapshotTick);
            }
        }

        public void Stop()
        {
            if (_context != null)
            {
                _context.Match.RulesChanged -= HandleRulesChanged;
                _context.Match.MapChanged -= HandleMapChanged;
                _context.Match.PlayerLeft -= HandlePlayerLeft;
            }

            _visibility = null;
            _seenThisTick.Clear();
            _clients.Clear();
        }

        private void HandleMapChanged(ServerMapData map)
        {
            _visibility?.UseWorld(map.World);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _visibility?.Forget(player.PlayerId);
        }

        // 目的を持つモジュールが無い試合でも、次の試合の目的とルールを載せる。持つモジュールは Contribute で同じ値を書き直す
        private void HandleRulesChanged(GameRules rules)
        {
            _rules = MatchRulesSync.From(rules);
            _objective = rules.Objective;
        }

        private void Fill(SnapshotMessage snapshot, int tick, ServerPlayer receiver)
        {
            var match = _context.Match;
            snapshot.Tick = tick;
            ref var status = ref snapshot.Match;
            status.State = match.State;
            status.AlphaScore = match.AlphaScore;
            status.BravoScore = match.BravoScore;
            status.NextMatchSeconds = (int)Math.Ceiling(match.RestartRemainingSeconds);
            status.Rules = _rules;
            status.Objective = _objective;
            snapshot.HasOwner = true;

            // 倒れて観戦している人にも生きている人と同じく全員の行を送るので、映す相手の行を別に足さない。
            // 映せるのは同じチームの人だけで、生きている味方が受け取る行より敵の情報は増えない
            foreach (var player in match.Players)
            {
                if (snapshot.Players.Count == snapshot.Players.MaxCount)
                {
                    break;
                }

                ref var row = ref snapshot.Players.Add();
                row.PlayerId = player.PlayerId;
                row.Team = player.Team;
                row.WeaponId = "";
            }

            foreach (var contributor in _contributors)
            {
                contributor.Contribute(snapshot, receiver);
            }

            // コントリビューターが書いた動きを後から空にするので、どれより後に置く
            if (_visibility != null)
            {
                ConcealUnseen(snapshot, receiver);
            }
        }

        private void ConcealUnseen(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            var receiverId = receiver.PlayerId;
            var teamWide = CollectEyes(receiver, out var leaderId);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == receiverId || _sides.AreAllies(receiverId, row.PlayerId))
                {
                    continue;
                }

                if (!IsSeen(teamWide, receiver.Team, leaderId, row.PlayerId))
                {
                    Conceal(ref row);
                }
            }
        }

        // 見る側のまとまりが受け取る人のチームと一致するなら true (ServerVisibility.CanSee のチームの覚え)。
        // 一致しない (FFA の一人、BR の分隊) なら、まとまりで一番小さい番号を leaderId に返す
        private bool CollectEyes(ServerPlayer receiver, out int leaderId)
        {
            _eyes.Clear();
            var receiverId = receiver.PlayerId;
            leaderId = receiverId;
            var teamWide = true;
            var session = _sides.Session;
            foreach (var player in _context.Match.Players)
            {
                var id = player.PlayerId;
                var allied = id == receiverId || _sides.AreAllies(receiverId, id);

                // まだ試合 (Session) に入っていない人は誰の味方でもないので、チームと一致するかの判定には数えない
                if (session != null && session.HasPlayer(id) && allied != (player.Team == receiver.Team))
                {
                    teamWide = false;
                }

                if (!allied)
                {
                    continue;
                }

                leaderId = Math.Min(leaderId, id);
                if (_sides.IsAlive(id) && _context.Bodies.TryGet(id, out var body) && body.HasSpawned)
                {
                    _eyes.Add(body.EyePosition);
                }
            }

            return teamWide;
        }

        private bool IsSeen(bool teamWide, Team team, int leaderId, int targetId)
        {
            var key = (teamWide, teamWide ? (int)team : leaderId, targetId);
            if (_seenThisTick.TryGetValue(key, out var seen))
            {
                return seen;
            }

            // 出現していない人は見える位置を持たないので、隠したままにする
            if (!_context.Bodies.TryGet(targetId, out var body) || !body.HasSpawned)
            {
                seen = false;
            }
            else if (teamWide)
            {
                seen = _visibility.CanSee(team, _eyes, targetId, body.Center, body.EyePosition, _now);
            }
            else
            {
                seen = _visibility.CanSeeAsGroup(leaderId, _eyes, targetId, body.Center, body.EyePosition, _now);
            }

            _seenThisTick[key] = seen;
            return seen;
        }

        // 名簿・スコア・生死 (体力・ダウン・出現の守り)・チームは残し、どこで何をしているかが分かる値だけを空にする。
        // 乗っている乗り物も消すのは、乗り物の行の位置から乗っている人の位置が分かるため
        private static void Conceal(ref PlayerSnapshot row)
        {
            row.Concealed = true;
            row.Position = Vector3.Zero;
            row.Velocity = Vector3.Zero;
            row.Yaw = 0f;
            row.IsGrounded = false;
            row.IsCrouched = false;
            row.IsProne = false;
            row.IsAiming = false;
            row.IsParachuting = false;
            row.Cover = default;
            row.Dodge = default;
            row.Vault = VaultSync.None;
            row.Prone = CharacterProneState.None;
            row.Ladder = CharacterLadderState.None;
            row.Vehicle = VehicleSeatSync.None;
            row.WeaponId = "";
            row.Lean = 0f;
            row.Swimming = false;
            row.WallRunSide = default;
            row.AirJumpsUsed = 0;
            row.JumpGraceLeft = 0f;
        }

        private sealed class ClientHistory
        {
            private readonly SnapshotMessage[] _sent = new SnapshotMessage[HistoryLength];
            private int _acked = InputMessage.NoSnapshot;

            public SnapshotMessage Next(int tick)
            {
                var slot = tick % HistoryLength;
                var snapshot = _sent[slot] ??= new SnapshotMessage();
                snapshot.Clear();
                return snapshot;
            }

            // 届いた順が入れ替わっても基準を古い方へ戻さない。送っていない tick を言われたら基準にしない
            public void Acknowledge(int tick)
            {
                if (tick > _acked && tick >= 0 && _sent[tick % HistoryLength]?.Tick == tick)
                {
                    _acked = tick;
                }
            }

            public SnapshotMessage Baseline(int tick)
            {
                if (_acked == InputMessage.NoSnapshot || _acked >= tick || tick - _acked >= HistoryLength)
                {
                    return null;
                }

                var baseline = _sent[_acked % HistoryLength];
                return baseline != null && baseline.Tick == _acked ? baseline : null;
            }
        }
    }
}
