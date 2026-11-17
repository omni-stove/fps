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
        public void ChatMapVoteAndSpectateRequestsDoNothingYet()
        {
            Start(new NetMatchSettings(mapPool: _maps, mapVoteSeconds: 5f));
            Join(1);
            Join(2);
            var sent = _transport.Sent.Count;
            var log = _log.ToString();
            var json = _json.ToString();

            _module.OnMessage(new ConnectionId(1), new ChatRequestMessage { Channel = Protocol.ChatChannel.All, Text = "hello" });
            _module.OnMessage(new ConnectionId(1), new ChatRequestMessage { Channel = Protocol.ChatChannel.Team, Text = "go" });
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
            Start(new NetMatchSettings(
                scoreLimit: 1, restartDelaySeconds: 1f, maxTeamSizeDifference: 1, mapPool: _maps, mapVoteSeconds: 0.5f));
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

        private void Start(NetMatchSettings settings)
        {
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
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

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
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
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }
    }
}
