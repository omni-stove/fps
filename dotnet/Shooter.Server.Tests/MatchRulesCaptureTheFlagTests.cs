using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // CTF はまだ ServerObjectiveFactory に載せていないので、目的の選び方を差し替えた MatchRulesModule で試合として通す
    public sealed class MatchRulesCaptureTheFlagTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaBase = new Vector3(-20f, 1f, 0f);
        private static readonly Vector3 _bravoBase = new Vector3(20f, 1f, 0f);
        private static readonly Vector3 _middle = new Vector3(0f, 1f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
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
        public void CarryingTheEnemyFlagHomeScoresAndTheScoreLimitEndsTheMatch()
        {
            Start(Spec(scoreLimit: 2));
            Join(1);
            Join(2);
            Step();
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Objective, Is.TypeOf<CaptureTheFlagRules>());
            Assert.That(Rules.Game.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_log.ToString(), Does.Contain(
                "[Mode] ctf net scoreLimit=2 timeLimit=120s respawn=1s returnTime=5s pickupRadius=1.5 captureRadius=2 pointsPerCapture=1"));
            Assert.That(Flag(Team.Bravo).State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(Flag(Team.Bravo).Position, Is.EqualTo(_bravoBase));

            MoveTo(1, _bravoBase);
            Step();

            var carried = Flag(Team.Bravo);
            Assert.That(carried.State, Is.EqualTo(ObjectiveItemState.Carried));
            Assert.That(carried.HasCarrier, Is.True);
            Assert.That(carried.CarrierId, Is.EqualTo(1));
            Assert.That(carried.Position, Is.EqualTo(_bravoBase));
            Assert.That(_log.ToString(), Does.Contain("[Mode] flag-taken flag=1 player=1"));

            MoveTo(1, _middle);
            Step();
            Assert.That(Flag(Team.Bravo).Position, Is.EqualTo(_middle), "a carried flag goes with its carrier");

            MoveTo(1, _alphaBase);
            Step();

            Assert.That(Match().AlphaPoints, Is.EqualTo(1));
            Assert.That(Match().BravoPoints, Is.Zero);
            Assert.That(_match.AlphaScore, Is.EqualTo(1));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(Flag(Team.Bravo).State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(Flag(Team.Bravo).Position, Is.EqualTo(_bravoBase));
            Assert.That(_log.ToString(), Does.Contain("[Mode] flag-captured flag=1 player=1 side=0 score=1"));

            MoveTo(1, _bravoBase);
            Step();
            MoveTo(1, _alphaBase);
            Step();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Rules.Game.Session.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.CaptureTheFlag));
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(2));
            Assert.That(_match.AlphaScore, Is.EqualTo(2));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\"").And.Contain("\"scoreAlpha\":\"2\""));
        }

        [Test]
        public void KillsDoNotScore()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            Assert.That(Row(1).Kills, Is.EqualTo(1));
            Assert.That(Match().AlphaPoints, Is.Zero);
            Assert.That(_match.AlphaScore, Is.Zero, "the match score counts captures, not kills");
        }

        [Test]
        public void KillingTheCarrierDropsTheFlagWhereTheyFellAndItReturnsOnItsOwn()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Step();
            MoveTo(1, _bravoBase);
            Step();
            MoveTo(1, _middle);
            Step();

            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            var dropped = Flag(Team.Bravo);
            Assert.That(dropped.State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(dropped.HasCarrier, Is.False);
            Assert.That(dropped.Position, Is.EqualTo(_middle));
            Assert.That(Match().ObjectiveRemainingSeconds, Is.EqualTo(5));
            Assert.That(_log.ToString(), Does.Contain("[Mode] flag-dropped flag=1 player=1"));

            Advance(2f);
            Assert.That(Match().ObjectiveRemainingSeconds, Is.EqualTo(3));

            Advance(3.1f);

            Assert.That(Flag(Team.Bravo).State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(Flag(Team.Bravo).Position, Is.EqualTo(_bravoBase));
            Assert.That(Match().ObjectiveRemainingSeconds, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Mode] flag-returned flag=1 by=-"));
        }

        [Test]
        public void ADefenderTouchingTheirDroppedFlagSendsItHome()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Step();
            MoveTo(1, _bravoBase);
            Step();
            MoveTo(1, _middle);
            Step();
            _module.ApplyDamage(2, 1, 100);
            Step();

            MoveTo(2, _middle);
            Step();

            Assert.That(Flag(Team.Bravo).State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(_log.ToString(), Does.Contain("[Mode] flag-returned flag=1 by=2"));
        }

        [Test]
        public void ADeadPlayerCannotPickUpAFlag()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Step();

            _module.ApplyDamage(2, 1, 100);
            MoveTo(1, _bravoBase);
            Step();

            Assert.That(_module.Session.IsAlive(1), Is.False);
            Assert.That(Flag(Team.Bravo).State, Is.EqualTo(ObjectiveItemState.Home));
        }

        [Test]
        public void ACarrierWhoLeavesDropsTheFlag()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Join(3);
            Step();
            MoveTo(1, _bravoBase);
            Step();
            MoveTo(1, _middle);
            Step();

            _match.Leave(new ConnectionId(1));
            Step();

            var dropped = Flag(Team.Bravo);
            Assert.That(dropped.State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(dropped.Position, Is.EqualTo(_middle));
            Assert.That(Rules.Game.Session.HasPlayer(1), Is.False);
        }

        [Test]
        public void TheCaptureTheFlagClockEndsTheMatchAndTheLeaderWins()
        {
            Start(Spec(scoreLimit: 3, timeLimitSeconds: 3f));
            Join(1);
            Join(2);
            Step();
            Assert.That(Match().RemainingSeconds, Is.EqualTo(3));

            MoveTo(1, _bravoBase);
            Step();
            MoveTo(1, _alphaBase);
            Step();
            Assert.That(Match().AlphaPoints, Is.EqualTo(1));

            Advance(3.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().HasWinner, Is.True);
            Assert.That(Match().Winner, Is.EqualTo(Team.Alpha));
            Assert.That(Match().RemainingSeconds, Is.Zero);
        }

        [Test]
        public void AnEvenTimeUpIsADraw()
        {
            Start(Spec(timeLimitSeconds: 2f));
            Join(1);
            Join(2);
            Step();

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().HasWinner, Is.False);
        }

        [Test]
        public void TheNextMatchStartsFromNoCapturesWithBothFlagsHome()
        {
            Start(Spec(scoreLimit: 1), restartDelaySeconds: 2f);
            Join(1);
            Join(2);
            Step();
            MoveTo(2, _alphaBase);
            Step();
            MoveTo(2, _bravoBase);
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_match.BravoScore, Is.EqualTo(1));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(Rules.Game.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.BravoScore, Is.Zero);
            Assert.That(Match().BravoPoints, Is.Zero);
            Assert.That(Flag(Team.Alpha).State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(Flag(Team.Bravo).State, Is.EqualTo(ObjectiveItemState.Home));
        }

        [Test]
        public void TheSnapshotCarriesBothFlags()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Step();

            var snapshot = new SnapshotMessage();
            _match.TryGetPlayer(1, out var receiver);
            _module.Contribute(snapshot, receiver);

            Assert.That(snapshot.Match.Objective, Is.EqualTo(MatchObjective.CaptureTheFlag));
            Assert.That(snapshot.ObjectiveItems.Count, Is.EqualTo(2));
            Assert.That(snapshot.ObjectiveItems[0].ItemId, Is.EqualTo(0));
            Assert.That(snapshot.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Flag));
            Assert.That(snapshot.ObjectiveItems[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.ObjectiveItems[0].Position, Is.EqualTo(_alphaBase));
            Assert.That(snapshot.ObjectiveItems[1].ItemId, Is.EqualTo(1));
            Assert.That(snapshot.ObjectiveItems[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(snapshot.ControlPoints.Count, Is.Zero);
        }

        [Test]
        public void AMapWithoutFlagBasesIsRefused()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Spec(), points: TestRules.Points()));

            Assert.That(e.Message, Is.EqualTo("cannot start the CaptureTheFlag match: map NetTest has no flag base for team Alpha"));
        }

        [Test]
        public void ARulesFactoryThatMakesNothingIsRefused()
        {
            _match = new ServerMatch(10f, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = Context(Settings(Spec(), 10f), Points());
            var module = new MatchRulesModule((objective, settings, host) => null);

            var e = Assert.Throws<InvalidOperationException>(() => module.Start(context));

            Assert.That(e.Message, Is.EqualTo("no rules were created for the match objective CaptureTheFlag"));
        }

        private CaptureTheFlagRules Rules => (CaptureTheFlagRules)_module.Objective;

        private static CaptureTheFlagSpec Spec(int scoreLimit = 3, float timeLimitSeconds = 120f)
        {
            return new CaptureTheFlagSpec(scoreLimit: scoreLimit, timeLimitSeconds: timeLimitSeconds, respawnSeconds: 1f, returnSeconds: 5f);
        }

        private static NetMatchSettings Settings(CaptureTheFlagSpec spec, float restartDelaySeconds)
        {
            return new NetMatchSettings(restartDelaySeconds: restartDelaySeconds, captureTheFlag: spec);
        }

        private static NetMapPoints Points()
        {
            return new NetMapPoints(
                TestRules.Points().SpawnPoints,
                Array.Empty<ControlPointArea>(),
                new[] { new FlagBasePoint(Team.Alpha, _alphaBase), new FlagBasePoint(Team.Bravo, _bravoBase) });
        }

        private static GameRules GameRulesFor()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(MatchObjective.CaptureTheFlag);
        }

        private ServerContext Context(NetMatchSettings settings, NetMapPoints points)
        {
            return new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", points),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(GameRulesFor(), settings));
        }

        private void Start(CaptureTheFlagSpec spec, float restartDelaySeconds = 10f, NetMapPoints points = null)
        {
            var settings = Settings(spec, restartDelaySeconds);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
            _context = Context(settings, points ?? Points());
            var module = new MatchRulesModule((objective, s, host) =>
                objective == MatchObjective.CaptureTheFlag ? new CaptureTheFlagRules(s, host) : ServerObjectiveFactory.Create(objective, s, host));
            module.Start(_context);
            _module = module;
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private void MoveTo(int id, Vector3 position)
        {
            _context.Bodies.Teleport(id, position, 0f, _tick);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private MatchSnapshot Match()
        {
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            return snapshot;
        }

        private PlayerSnapshot Row(int id)
        {
            var row = default(PlayerSnapshot);
            Assert.That(_module.FillPlayer(id, ref row), Is.True);
            return row;
        }

        private ObjectiveItemSnapshot Flag(Team team)
        {
            var items = new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
            _module.FillObjectiveItems(items);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Kind == ObjectiveItemKind.Flag && items[i].Team == team)
                {
                    return items[i];
                }
            }

            Assert.Fail("no flag of team " + team + " is in the snapshot");
            return default;
        }

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Step();
            }
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }
    }
}
