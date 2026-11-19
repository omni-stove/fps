using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 次の試合のルール (ServerMatch.RequestNextRules) を受けたら、目的と Session をそのルールで作り直して次の試合を始める。
    // Session を Restart せず作り直すのは、点数・時間の上限とダウンの有無を Session が作るときにしか受け取らないため。
    internal sealed partial class MatchRulesModule
    {
        // 次の試合のルールが替わるときだけ持つ。同じルールを渡されたときは null のままにして、今までどおり Session を Restart する
        private ServerRulesData _nextData;

        // 目的と Session を作り直した後、全員を出現させる前に出す。持ち替え (PlayerSpawned) で新しいルールの武器と数を使わせるため
        public event Action<ServerRulesData> RulesChanged;

        public ServerRulesData Data => _data;

        private TeamDeathmatchSession CreateSession(ServerMapData map, NetMapPoints points)
        {
            var settings = _data.Settings;
            try
            {
                return new TeamDeathmatchSession(
                    _objective.CreateSessionSpec(_data.Rules, settings),
                    settings.MaxHealth,
                    points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds,
                    _data.Assist);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(
                    "cannot start the match on " + map.Name + " (" + points.SpawnPoints.Count.ToString(CultureInfo.InvariantCulture)
                    + " spawn points): " + e.Message, e);
            }
        }

        private void ListenToSession(TeamDeathmatchSession session)
        {
            session.Started += HandleStarted;
            session.Restarted += HandleRestarted;
            session.ScoreChanged += HandleScoreChanged;
            session.Killed += HandleKilled;
            session.Spawned += HandleSpawned;
            session.Ended += HandleEnded;
            session.HeldExpired += HandleHeldExpired;
            session.Downed += HandleDowned;
            session.Revived += HandleRevived;
            session.DownedChanged += HandleDownedChanged;
        }

        private void IgnoreSession(TeamDeathmatchSession session)
        {
            session.Started -= HandleStarted;
            session.Restarted -= HandleRestarted;
            session.ScoreChanged -= HandleScoreChanged;
            session.Killed -= HandleKilled;
            session.Spawned -= HandleSpawned;
            session.Ended -= HandleEnded;
            session.HeldExpired -= HandleHeldExpired;
            session.Downed -= HandleDowned;
            session.Revived -= HandleRevived;
            session.DownedChanged -= HandleDownedChanged;
        }

        // 試合の合間に作れないと分かると遊んでいる人を残して止めるしかないので、受け付けるときに目的を作り、替わりうるマップを全部確かめる
        private string CheckNextRules(GameRules rules)
        {
            if (ReferenceEquals(rules, _data.Rules))
            {
                return null;
            }

            try
            {
                var data = _data.WithRules(rules);
                var objective = _createObjective(rules.Objective, data.Settings, this)
                    ?? throw new InvalidOperationException("no rules were created for the match objective " + rules.Objective);
                foreach (var map in MapsToCheck())
                {
                    var points = PointsOf(map);
                    RequireSpawnPointFor(map, points, Team.Alpha);
                    RequireSpawnPointFor(map, points, Team.Bravo);
                    objective.CheckMap(map, points);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return "cannot use the rules with objective " + rules.Objective + " for the next match: " + e.Message;
            }

            return null;
        }

        private IEnumerable<ServerMapData> MapsToCheck()
        {
            return _match.Maps.Count > 0 ? _match.Maps : new[] { _context.Map };
        }

        private void HandleRulesChanged(GameRules rules)
        {
            _nextData = ReferenceEquals(rules, _data.Rules) ? null : _data.WithRules(rules);
        }

        private void UseNextRules()
        {
            var data = _nextData;
            _nextData = null;
            var from = _data.Rules.Objective;
            var map = _context.Map;
            var points = PointsOf(map);

            StopObjective();
            IgnoreSession(_session);
            ClearMatchState();
            SpectateMatchRestarted();

            _data = data;
            var rules = data.Rules;
            _objective = _createObjective(rules.Objective, data.Settings, this)
                ?? throw new InvalidOperationException("no rules were created for the match objective " + rules.Objective);
            PrepareGunGame();
            _objective.UseMap(map, points);
            _session = CreateSession(map, points);
            ListenToSession(_session);

            _log.WriteLine(MatchLog.RulesChanged(from, rules.Objective));
            _log.WriteLine(MatchLog.Rules(_data.File.Name.Length > 0 ? _data.File.Name : _data.Name, rules));
            _log.WriteLine(NetDownedLog.Settings(rules.Actions, rules.Actions.ReviveRange));
            PreparePings(rules);
            _idleTracker = null;
            PrepareIdle(rules);
            StartObjective();
            RulesChanged?.Invoke(data);

            // 試合の枠のチーム (並べ直した後) のまま入れ直す。2 チームそろった人で Session が始まり、MatchStarted が出る
            var players = new List<ServerPlayer>(_match.Players);
            foreach (var player in players)
            {
                _session.Join(player.PlayerId, KeyOf(player.RejoinKey), player.Team);
                IdlePlayerJoined(player.PlayerId);
                ObjectivePlayerJoined(player.PlayerId);
            }
        }
    }
}
