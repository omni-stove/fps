using System;
using System.Collections.Generic;
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
        private ServerContext _context;

        public ServerSnapshots(GameRules rules, IEnumerable<ISnapshotContributor> contributors, AntiCheatTuning antiCheat = null)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(contributors);
            AntiCheat = antiCheat ?? AntiCheatTuning.Default;
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
        }

        public void Tick(long tick, float deltaSeconds)
        {
            // 1 秒 60 tick なら 1 年以上かかる。そこまで続けば、試合を区切って始め直すほうが先に来る
            if (tick > int.MaxValue)
            {
                throw new InvalidOperationException("the server tick no longer fits a snapshot tick");
            }

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
            }

            _clients.Clear();
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
