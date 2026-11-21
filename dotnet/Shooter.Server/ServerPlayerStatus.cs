using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // スタミナ・よろけ・制圧をサーバーで持ち、自分の値をスナップショットの持ち主の欄で送る。判定は Core の PlayerStatusMeters に任せる。
    // 移動 (ServerMovement.InputGate) が入力を絞らせて動いた後を知らせ、戦闘 (ServerCombat.PlayerStatus) が被弾と弾の通り道を知らせる。
    // どれもルールの actions で Off なら何もしない
    internal sealed class ServerPlayerStatus : IServerModule, ISnapshotContributor, IServerInputGate
    {
        private readonly Dictionary<int, Entry> _players = new Dictionary<int, Entry>();
        private readonly List<int> _ids = new List<int>();
        private readonly MatchRulesModule _sides;
        private ActionRules _actions;
        private ServerContext _context;
        private TextWriter _log = TextWriter.Null;

        // 生死と出現し直しはルールのモジュールが持つので、無ければ全員を生きているとみなし、出現での満タンを行わない
        public ServerPlayerStatus(GameRules rules, ServerPlayerStatusTuning tuning = null, MatchRulesModule sides = null)
        {
            ArgumentNullException.ThrowIfNull(rules);
            _actions = rules.Actions;
            Tuning = tuning ?? ServerPlayerStatusTuning.Default;
            _sides = sides;
        }

        public string Name => "player-status";

        public ServerPlayerStatusTuning Tuning { get; }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _context.Match.RulesChanged += HandleRulesChanged;
            _context.Match.PlayerLeft += HandlePlayerLeft;
            if (_sides != null)
            {
                _sides.PlayerSpawned += HandlePlayerSpawned;
            }

            _log.WriteLine(Tuning.LogLine);
            _log.WriteLine(ServerPlayerStatusLog.Rules(_actions));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _ids.Clear();
            _ids.AddRange(_players.Keys);
            foreach (var id in _ids)
            {
                var entry = _players[id];
                if (!IsAlive(id))
                {
                    entry.Meters.Reset();
                    Settle(id, entry, false);
                    continue;
                }

                var ended = entry.Meters.Tick(deltaSeconds);
                if (ended != StaggerKind.None)
                {
                    _log.WriteLine(ServerPlayerStatusLog.StaggerRecovered(id, ended));
                }

                Settle(id, entry, true);
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            if (_context != null)
            {
                _context.Match.RulesChanged -= HandleRulesChanged;
                _context.Match.PlayerLeft -= HandlePlayerLeft;
            }

            if (_sides != null)
            {
                _sides.PlayerSpawned -= HandlePlayerSpawned;
            }

            _players.Clear();
        }

        // ほかの人の値は送らない。よろけの見た目を他の人に見せるのは、アニメの同期を持つ側の役目にする
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(receiver);
            var meters = GetOrAdd(receiver.PlayerId).Meters;
            var owner = snapshot.Owner;
            owner.Stamina = meters.StaminaStatus;
            owner.Stagger = meters.StaggerKind;
            owner.SuppressionLevel = meters.SuppressionLevel;
            owner.Suppressed = meters.IsSuppressed;
        }

        public CharacterMotorInput Restrain(int playerId, CharacterMotorInput input)
        {
            return GetOrAdd(playerId).Meters.Restrain(input);
        }

        public void Stepped(int playerId, CharacterMotorInput input, CharacterMotorState previous, CharacterMotorState current, float deltaSeconds)
        {
            var entry = GetOrAdd(playerId);
            entry.Meters.Stepped(input, previous, current, deltaSeconds);
            Settle(playerId, entry, true);
        }

        public bool TryGetMeters(int playerId, out PlayerStatusMeters meters)
        {
            if (_players.TryGetValue(playerId, out var entry))
            {
                meters = entry.Meters;
                return true;
            }

            meters = null;
            return false;
        }

        // 倒れている人には数えない。倒したダメージは戦闘の側が渡さない
        public void Damaged(int playerId, HitZone? zone, DamageKind kind, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (!IsAlive(playerId))
            {
                return;
            }

            var verdict = GetOrAdd(playerId).Meters.Damaged(zone, kind, amount);
            if (!verdict.HasValue)
            {
                return;
            }

            switch (verdict.Value.Outcome)
            {
                case StaggerOutcome.Staggered:
                    _log.WriteLine(ServerPlayerStatusLog.Staggered(playerId, verdict.Value, zone, kind));
                    break;
                case StaggerOutcome.Immune:
                    _log.WriteLine(ServerPlayerStatusLog.StaggerResisted(playerId, verdict.Value.Kind));
                    break;
            }
        }

        // 撃った人と、この弾が当たった人は数えない。味方も数えるのはオフラインの Hitscan と同じ
        public void ShotPassed(int shooterId, Vector3 origin, Vector3 direction, float length, IReadOnlySet<int> struck)
        {
            ArgumentNullException.ThrowIfNull(struck);
            if (_context == null || _actions.Suppression != Suppression.On
                || !(direction.LengthSquared() > 1e-12f) || !(length >= 0f) || float.IsInfinity(length))
            {
                return;
            }

            foreach (var body in _context.Bodies.All)
            {
                var id = body.PlayerId;
                if (id == shooterId || struck.Contains(id) || !body.HasSpawned || !IsAlive(id))
                {
                    continue;
                }

                var entry = GetOrAdd(id);
                if (entry.Meters.AddNearMiss(origin, direction, length, body.Center, out _))
                {
                    Settle(id, entry, true);
                }
            }
        }

        private bool IsAlive(int playerId)
        {
            return _sides == null || _sides.IsAlive(playerId);
        }

        private Entry GetOrAdd(int playerId)
        {
            if (!_players.TryGetValue(playerId, out var entry))
            {
                entry = new Entry(new PlayerStatusMeters(_actions, Tuning.Stamina, Tuning.Stagger.Player, Tuning.Suppression));
                _players.Add(playerId, entry);
            }

            return entry;
        }

        // 毎 tick 出すと読めないので、尽きた・戻った・制圧の入り抜けだけを出す。倒れて数え直したときは出さない
        private void Settle(int playerId, Entry entry, bool report)
        {
            var meters = entry.Meters;
            var depleted = meters.IsStaminaOn && meters.StaminaMeter.IsDepleted;
            if (report && depleted != entry.WasDepleted)
            {
                var status = meters.StaminaMeter.Status;
                _log.WriteLine(depleted
                    ? ServerPlayerStatusLog.StaminaDepleted(playerId, status)
                    : ServerPlayerStatusLog.StaminaRecovered(playerId, status));
            }

            var suppressed = meters.IsSuppressed;
            if (report && suppressed != entry.WasSuppressed)
            {
                _log.WriteLine(ServerPlayerStatusLog.SuppressedChanged(playerId, suppressed, meters.SuppressionLevel));
            }

            entry.WasDepleted = depleted;
            entry.WasSuppressed = suppressed;
        }

        private void HandlePlayerSpawned(int playerId)
        {
            var entry = GetOrAdd(playerId);
            entry.Meters.Reset();
            Settle(playerId, entry, false);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _players.Remove(player.PlayerId);
        }

        // 調整値はファイルを読み直さず起動時のものを使い、ルールだけを替える。試合をまたいで減った量やためた度合いを持ち越さない
        private void HandleRulesChanged(GameRules rules)
        {
            _actions = rules.Actions;
            foreach (var pair in _players)
            {
                pair.Value.Meters.Configure(_actions, Tuning.Stamina, Tuning.Stagger.Player, Tuning.Suppression);
                Settle(pair.Key, pair.Value, false);
            }

            _log.WriteLine(ServerPlayerStatusLog.Rules(_actions));
        }

        private sealed class Entry
        {
            public Entry(PlayerStatusMeters meters)
            {
                Meters = meters;
            }

            public PlayerStatusMeters Meters { get; }

            public bool WasDepleted { get; set; }

            public bool WasSuppressed { get; set; }
        }
    }
}
