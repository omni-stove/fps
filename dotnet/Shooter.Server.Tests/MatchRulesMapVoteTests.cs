using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesMapVoteTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _firstMap = "NetTest";
        private const string _secondMap = "NetArena01";
        private const string _thirdMap = "NetArena02";
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

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
        public void VotesDecideTheMapOfTheNextMatch()
        {
            Start(restartDelaySeconds: 2f, mapVoteSeconds: 2f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            var ended = _match.MatchId;

            // 今のマップは候補に出さず、残りはマップの候補の並びのまま出す
            Assert.That(_module.CurrentMapVote.Candidates, Is.EqualTo(new[] { _secondMap, _thirdMap }));
            Assert.That(_module.ServerMapVote(1, 1), Is.EqualTo(MapVoteCast.Cast));
            Assert.That(_module.ServerMapVote(2, 0), Is.EqualTo(MapVoteCast.Cast));
            Assert.That(_module.ServerMapVote(2, 1), Is.EqualTo(MapVoteCast.Changed));
            Advance(2f + 0.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_thirdMap));
            var log = _log.ToString();
            Assert.That(log, Does.Contain(MapVoteLog.Cast(1, _thirdMap, MapVoteCast.Cast)));
            Assert.That(log, Does.Contain(MapVoteLog.Cast(2, _thirdMap, MapVoteCast.Changed)));
            Assert.That(log, Does.Contain(MapVoteLog.Decided(new MapVoteDecision(_thirdMap, MapVoteDecisionReason.Majority, 2))));
            Assert.That(log, Does.Contain(MatchLog.MapChanged(_firstMap, _thirdMap)));
            Assert.That(JsonOf(ServerEventLog.MapVoteType), Is.EqualTo(new[]
            {
                ServerEventLog.MapVote(_time, ended, 1, null, _thirdMap),
                ServerEventLog.MapVote(_time, ended, 2, null, _secondMap),
                ServerEventLog.MapVote(_time, ended, 2, null, _thirdMap),
            }));
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Is.EqualTo(new[]
            {
                ServerEventLog.MapChanged(_time, ended, _firstMap, _thirdMap),
            }));

            var decided = States(2).Last();
            Assert.That(decided.CandidateCount, Is.EqualTo(2));
            Assert.That(decided.MapId(1), Is.EqualTo(_thirdMap));
            Assert.That(decided.Votes(1), Is.EqualTo(2));
            Assert.That(decided.Votes(0), Is.EqualTo(0));
            Assert.That(decided.ChosenIndex, Is.EqualTo(1));
            Assert.That(decided.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(_module.CurrentMapVote, Is.Null, "the next match has started");
        }

        [Test]
        public void TwoMapsOfferTheOtherMapAndChangeToIt()
        {
            Start(restartDelaySeconds: 1f, mapVoteSeconds: 1f, _firstMap, _secondMap);
            Join(1);
            Join(2);
            EndTheMatch();

            var opened = States(1).Single();
            Assert.That(opened.CandidateCount, Is.EqualTo(1));
            Assert.That(opened.MapId(0), Is.EqualTo(_secondMap));
            Assert.That(opened.ChosenIndex, Is.EqualTo(MapVoteStateMessage.NotChosen));
            Assert.That(opened.RemainingSeconds, Is.EqualTo(1f));
            Advance(1f + 0.1f);

            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(_log.ToString(), Does.Contain(MapVoteLog.Decided(new MapVoteDecision(_secondMap, MapVoteDecisionReason.NoVotes, 0))));
            Assert.That(JsonOf(ServerEventLog.MapVoteType), Is.Empty);
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Has.Count.EqualTo(1));
        }

        [Test]
        public void OneMapSendsNoVote()
        {
            Start(restartDelaySeconds: 1f, mapVoteSeconds: 5f, _firstMap);
            Join(1);
            Join(2);
            Assert.That(_match.HoldsMapVote, Is.False);
            Assert.That(_match.RestartDelaySeconds, Is.EqualTo(1f), "with nothing to vote for the wait is not lengthened");

            EndTheMatch();
            Assert.That(_module.CurrentMapVote, Is.Null);
            Assert.That(_module.ServerMapVote(1, 0), Is.Null);
            Advance(1f + 0.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(States(1), Is.Empty);
            Assert.That(States(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Not.Contain(MapVoteLog.Tag));
            Assert.That(JsonOf(ServerEventLog.MapVoteType), Is.Empty);
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Is.Empty);
        }

        [Test]
        public void ZeroVoteSecondsHoldNoVote()
        {
            Start(restartDelaySeconds: 1f, mapVoteSeconds: 0f, _firstMap, _secondMap);
            Join(1);
            Join(2);

            EndTheMatch();
            Advance(1f + 0.1f);

            Assert.That(_match.HoldsMapVote, Is.False);
            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(States(1), Is.Empty);
        }

        [Test]
        public void TheNextMatchWaitsUntilTheVoteCloses()
        {
            Start(restartDelaySeconds: 1f, mapVoteSeconds: 3f, _firstMap, _secondMap);
            Join(1);
            Join(2);
            Assert.That(_match.RestartDelaySeconds, Is.EqualTo(3f));

            EndTheMatch();
            Assert.That(_log.ToString(), Does.Contain(MatchLog.NextMatch(3f)));
            Advance(1f + 0.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_module.CurrentMapVote.IsClosed, Is.False);

            Advance(2f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
        }

        [Test]
        public void ALongerWaitKeepsItsOwnSeconds()
        {
            Start(restartDelaySeconds: 4f, mapVoteSeconds: 1f, _firstMap, _secondMap);

            Assert.That(_match.RestartDelaySeconds, Is.EqualTo(4f));
        }

        [Test]
        public void TheStateIsSentAgainEverySecond()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 3f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            Assert.That(States(1), Has.Count.EqualTo(1));

            Advance(1.05f);
            var states = States(1);
            Assert.That(states, Has.Count.EqualTo(2));
            Assert.That(states[1].RemainingSeconds, Is.EqualTo(2f).Within(0.05f));
            Assert.That(states[1].ChosenIndex, Is.EqualTo(MapVoteStateMessage.NotChosen));

            _module.ServerMapVote(2, 0);
            states = States(1);
            Assert.That(states, Has.Count.EqualTo(3), "a new vote is sent at once");
            Assert.That(states[2].Votes(0), Is.EqualTo(1));
        }

        [Test]
        public void SameVoteAgainIsNotSentOrLogged()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 3f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            _module.ServerMapVote(1, 0);
            var sent = _transport.Sent.Count;

            Assert.That(_module.ServerMapVote(1, 0), Is.EqualTo(MapVoteCast.Unchanged));

            Assert.That(_transport.Sent, Has.Count.EqualTo(sent));
            Assert.That(JsonOf(ServerEventLog.MapVoteType), Has.Count.EqualTo(1));
        }

        [Test]
        public void AnIndexOutsideTheCandidatesIsRefused()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 3f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            var sent = _transport.Sent.Count;

            Assert.That(_module.ServerMapVote(1, 2), Is.EqualTo(MapVoteCast.NotCandidate));
            Assert.That(_module.ServerMapVote(1, -1), Is.EqualTo(MapVoteCast.NotCandidate));

            Assert.That(_transport.Sent, Has.Count.EqualTo(sent));
            Assert.That(JsonOf(ServerEventLog.MapVoteType), Is.Empty);
            Assert.That(_module.CurrentMapVote.VoteCount, Is.EqualTo(0));
            // 2 度目は間引かれて 1 行だけ残る
            Assert.That(Lines().Count(l => l.Contains(MapVoteLog.Tag + " cast", StringComparison.Ordinal)), Is.EqualTo(1));
        }

        [Test]
        public void AVoteAfterTheDeadlineIsRefused()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 1f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            Advance(1f + 0.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_module.CurrentMapVote.IsClosed, Is.True);
            var chosen = _match.NextMap;

            Assert.That(_module.ServerMapVote(1, 0), Is.EqualTo(MapVoteCast.Closed));
            Assert.That(_module.ServerMapVote(1, 1), Is.EqualTo(MapVoteCast.Closed));

            Assert.That(_match.NextMap, Is.EqualTo(chosen));
            Assert.That(States(1).Last().ChosenIndex, Is.Not.EqualTo(MapVoteStateMessage.NotChosen));
        }

        [Test]
        public void ALeaversVoteIsDroppedAndTheOthersAreTold()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 3f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            Join(3);
            EndTheMatch();
            _module.ServerMapVote(3, 1);
            Assert.That(States(1).Last().Votes(1), Is.EqualTo(1));

            Assert.That(_match.Leave(new ConnectionId(3)), Is.True);

            Assert.That(_module.CurrentMapVote.VoteCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain(MapVoteLog.Left(3, _thirdMap)));
            Assert.That(States(1).Last().Votes(1), Is.EqualTo(0));
        }

        [Test]
        public void SomeoneJoiningDuringTheVoteIsSentIt()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 3f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            _module.ServerMapVote(1, 0);

            Join(3);

            var state = States(3).Single();
            Assert.That(state.CandidateCount, Is.EqualTo(2));
            Assert.That(state.Votes(0), Is.EqualTo(1));
            Assert.That(_module.ServerMapVote(3, 1), Is.EqualTo(MapVoteCast.Cast));
        }

        [Test]
        public void ARequestMessageCastsTheVote()
        {
            Start(restartDelaySeconds: 3f, mapVoteSeconds: 3f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();

            _module.OnMessage(new ConnectionId(1), new MapVoteRequestMessage { MapIndex = 1 });

            Assert.That(_module.CurrentMapVote.VoteOf(1), Is.EqualTo(_thirdMap));
        }

        [Test]
        public void TheVoteIsClosedBeforeTheMapChangesWhenBothAreDueTogether()
        {
            Start(restartDelaySeconds: 1f, mapVoteSeconds: 1f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            EndTheMatch();
            _module.ServerMapVote(1, 0);
            var ended = _match.MatchId;

            // 試合の枠だけを進め、モジュールが締め切る前に次の試合を始めさせる
            for (var elapsed = 0f; elapsed < 1.1f; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
            }

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(_log.ToString(), Does.Contain(MapVoteLog.Decided(new MapVoteDecision(_secondMap, MapVoteDecisionReason.Majority, 1))));
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Is.EqualTo(new[]
            {
                ServerEventLog.MapChanged(_time, ended, _firstMap, _secondMap),
            }));
            Assert.That(_module.CurrentMapVote, Is.Null);
        }

        [Test]
        public void ASecondMatchOpensAFreshVote()
        {
            Start(restartDelaySeconds: 1f, mapVoteSeconds: 1f, _firstMap, _secondMap);
            Join(1);
            Join(2);
            EndTheMatch();
            Advance(1f + 0.1f);
            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));

            EndTheMatch();

            Assert.That(_module.CurrentMapVote.Candidates, Is.EqualTo(new[] { _firstMap }));
            Assert.That(_module.CurrentMapVote.VoteCount, Is.EqualTo(0));
            Advance(1f + 0.1f);
            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
        }

        [Test]
        public void TheSeedComesFromTheMatchIdTheSameWayEveryTime()
        {
            // FNV-1a (32 ビット) の "a" の値
            Assert.That(MatchRulesModule.SeedOf("a"), Is.EqualTo(unchecked((int)0xE40C292Cu)));
            var first = MatchRulesModule.SeedOf("match-1");
            var again = MatchRulesModule.SeedOf("match-1");
            Assert.That(again, Is.EqualTo(first));
            Assert.That(MatchRulesModule.SeedOf("match-2"), Is.Not.EqualTo(first));
        }

        private void Start(float restartDelaySeconds, float mapVoteSeconds, params string[] maps)
        {
            var settings = new NetMatchSettings(
                scoreLimit: 1, restartDelaySeconds: restartDelaySeconds, mapPool: maps, mapVoteSeconds: mapVoteSeconds);
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(
                settings.RestartDelaySeconds, _log, events, () => _time, settings.MaxTeamSizeDifference, settings.MapVoteSeconds);
            var loaded = maps.Select(name => TestMaps.Data(name, TestRules.Points())).ToArray();
            _match.UseMaps(loaded[0], loaded);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                loaded[0],
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

        // 1 は Alpha、2 は Bravo に入り、1 点で試合が終わる
        private void EndTheMatch()
        {
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _module.Tick(++_tick, _tickSeconds);
            }
        }

        private List<MapVoteStateMessage> States(int connection)
        {
            return _transport.Read<MapVoteStateMessage>(connection, Copy);
        }

        private static MapVoteStateMessage Copy(MapVoteStateMessage message)
        {
            var copy = new MapVoteStateMessage();
            for (var i = 0; i < message.CandidateCount; i++)
            {
                copy.AddCandidate(message.MapId(i), message.Votes(i));
            }

            copy.RemainingSeconds = message.RemainingSeconds;
            copy.ChosenIndex = message.ChosenIndex;
            return copy;
        }

        private List<string> Lines()
        {
            return _log.ToString().Split('\n').Select(line => line.TrimEnd('\r')).ToList();
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
