using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesBetweenMatchesTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        private static readonly string[] _maps = { "NetTest", "NetArena02" };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void MapVoteAndSpectateRequestsDuringPlayDoNothing()
        {
            Start(new NetMatchSettings(mapPool: _maps, mapVoteSeconds: 5f));
            Join(1);
            Join(2);
            var sent = _transport.Sent.Count;
            var log = _log.ToString();
            var json = _json.ToString();

            _module.OnMessage(new ConnectionId(2), new MapVoteRequestMessage { MapIndex = 1 });
            _module.OnMessage(new ConnectionId(2), new SpectateRequestMessage { Step = SpectateStep.Next });

            Assert.That(_transport.Sent, Has.Count.EqualTo(sent));
            Assert.That(_log.ToString(), Is.EqualTo(log));
            Assert.That(_json.ToString(), Is.EqualTo(json));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void RequestsFromSomeoneNotInTheMatchAreIgnored()
        {
            Start(NetMatchSettings.Default);
            Join(1);
            var sent = _transport.Sent.Count;

            _module.OnMessage(new ConnectionId(9), new ChatRequestMessage { Channel = Protocol.ChatChannel.All, Text = "hello" });

            Assert.That(_transport.Sent, Has.Count.EqualTo(sent));
        }

        [Test]
        public void TwoMatchesInARowKeepTheTeamsAndWriteNoNewEvents()
        {
            // 3 対 1 (差 2) が上限に収まるので、人数をそろえずに同じチームのまま続く
            Start(new NetMatchSettings(
                scoreLimit: 1, restartDelaySeconds: 1f, maxTeamSizeDifference: 2, mapPool: _maps, mapVoteSeconds: 0.5f));
            for (var id = 1; id <= 5; id++)
            {
                Join(id);
            }

            var smaller = _match.CountPlayers(Team.Alpha) < _match.CountPlayers(Team.Bravo) ? Team.Alpha : Team.Bravo;
            Assert.That(_match.CountPlayers(smaller), Is.EqualTo(2));
            _match.Leave(new ConnectionId((ulong)_match.Players.First(p => p.Team == smaller).PlayerId));
            var teams = Teams();
            var larger = smaller == Team.Alpha ? Team.Bravo : Team.Alpha;
            var killer = _match.Players.First(p => p.Team == larger).PlayerId;
            var victim = _match.Players.First(p => p.Team == smaller).PlayerId;
            var firstMatch = _match.MatchId;

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.ApplyDamage(killer, victim, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.MatchId, Is.Not.EqualTo(firstMatch));
            Assert.That(Teams(), Is.EqualTo(teams));
            Assert.That(_module.ApplyDamage(victim, killer, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(Teams(), Is.EqualTo(teams));
            Assert.That(_match.CountPlayers(larger) - _match.CountPlayers(smaller), Is.EqualTo(2));
            Assert.That(JsonOf(ServerEventLog.MatchStartedType), Has.Count.EqualTo(3));
            Assert.That(JsonOf(ServerEventLog.MatchEndedType), Has.Count.EqualTo(2));
            Assert.That(JsonOf(ServerEventLog.ChatType), Is.Empty);
            Assert.That(JsonOf(ServerEventLog.MapVoteType), Is.Empty);
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Is.Empty);
            Assert.That(JsonOf(ServerEventLog.TeamBalancedType), Is.Empty);
            Assert.That(_transport.Read<MapVoteStateMessage>(1, m => m), Is.Empty);
            Assert.That(_transport.Read<ChatMessage>(1, m => m), Is.Empty);
        }

        [Test]
        public void FourAgainstOneIsEvenedOutBeforeTheNextMatch()
        {
            Start(new NetMatchSettings(scoreLimit: 1, restartDelaySeconds: 1f, maxTeamSizeDifference: 1));
            for (var id = 1; id <= 8; id++)
            {
                Join(id);
            }

            // 奇数が Alpha、偶数が Bravo に入る。Bravo を 2 だけ残して 4 対 1 にする
            Leave(4);
            Leave(6);
            Leave(8);
            Assert.That(_match.CountPlayers(Team.Alpha), Is.EqualTo(4));
            Assert.That(_match.CountPlayers(Team.Bravo), Is.EqualTo(1));
            var firstMatch = _match.MatchId;

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(JsonOf(ServerEventLog.TeamBalancedType), Is.Empty);
            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.CountPlayers(Team.Alpha), Is.EqualTo(3));
            Assert.That(_match.CountPlayers(Team.Bravo), Is.EqualTo(2));
            Assert.That(TeamOf(7), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Session.GetTeam(7), Is.EqualTo(Team.Bravo));
            Assert.That(SnapshotTeamOf(7), Is.EqualTo(Team.Bravo));
            Assert.That(SnapshotTeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(JsonOf(ServerEventLog.TeamBalancedType), Is.EqualTo(new[]
            {
                ServerEventLog.TeamBalanced(_time, firstMatch, 7, null, Team.Alpha, Team.Bravo),
            }));
            Assert.That(_log.ToString(), Does.Contain("[Match] balanced player=7 from=Alpha to=Bravo"));

            // 移った人は次の試合で前の味方の敵になる
            Assert.That(_module.ApplyDamage(1, 7, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(JsonOf(ServerEventLog.KillType).Last(), Does.Contain("\"teamKill\":\"false\""));
        }

        [Test]
        public void APartyIsMovedTogether()
        {
            Start(new NetMatchSettings(scoreLimit: 1, restartDelaySeconds: 1f, maxTeamSizeDifference: 1));
            Join(1);
            Join(2);
            Join(3, "party-a");
            Join(4);
            Join(5);
            Join(6);
            Join(7, "party-a");
            Join(8);
            Leave(4);
            Leave(6);
            Leave(8);

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Advance(1.1f);

            // 最近入った 7 のパーティ (3・7) を一緒に移し、4 対 1 が 2 対 3 になる
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(TeamOf(3), Is.EqualTo(Team.Bravo));
            Assert.That(TeamOf(7), Is.EqualTo(Team.Bravo));
            Assert.That(TeamOf(5), Is.EqualTo(Team.Alpha));
            Assert.That(_match.CountPlayers(Team.Alpha), Is.EqualTo(2));
            Assert.That(_match.CountPlayers(Team.Bravo), Is.EqualTo(3));
            Assert.That(JsonOf(ServerEventLog.TeamBalancedType), Has.Count.EqualTo(2));
        }

        [Test]
        public void ALimitOfZeroLeavesFourAgainstOne()
        {
            Start(new NetMatchSettings(scoreLimit: 1, restartDelaySeconds: 1f));
            for (var id = 1; id <= 8; id++)
            {
                Join(id);
            }

            Leave(4);
            Leave(6);
            Leave(8);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.CountPlayers(Team.Alpha), Is.EqualTo(4));
            Assert.That(_match.CountPlayers(Team.Bravo), Is.EqualTo(1));
            Assert.That(JsonOf(ServerEventLog.TeamBalancedType), Is.Empty);
        }

        private void Leave(int id)
        {
            Assert.That(_match.Leave(new ConnectionId((ulong)id)), Is.True);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private Team SnapshotTeamOf(int id)
        {
            var row = default(PlayerSnapshot);
            Assert.That(_module.FillPlayer(id, ref row), Is.True);
            return row.Team;
        }

        private void Start(NetMatchSettings settings)
        {
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time, settings.MaxTeamSizeDifference);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(rules, settings));
            var module = new MatchRulesModule();
            module.Start(context);
            _module = module;
        }

        private void Join(int id, string partyId = null)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id, null, partyId);
        }

        private Team[] Teams()
        {
            return _match.Players.Select(p => p.Team).ToArray();
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _module.Tick(++_tick, _tickSeconds);
            }
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal))
                .ToList();
        }
    }
}
