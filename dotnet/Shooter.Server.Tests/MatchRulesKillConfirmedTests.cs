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
    public sealed class MatchRulesKillConfirmedTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _field = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _otherField = new Vector3(5f, 1f, 5f);
        private static readonly Vector3 _farAway = new Vector3(-40f, 1f, 40f);

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
        public void ConfirmedTagsScoreUntilTheScoreLimitEndsTheMatch()
        {
            Start(Spec(scoreLimit: 2));
            Join(1);
            Join(2);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Objective, Is.TypeOf<KillConfirmedRules>());
            Assert.That(
                _log.ToString(),
                Does.Contain("[Mode] killconfirmed net scoreLimit=2 timeLimit=60s respawn=0.5s tagLifetime=30s pointsPerConfirm=1 tagPickupRadius=1"));
            Step();

            Place(2, _field);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed drop tag=1 owner=2 side=1"));
            var items = Items();
            Assert.That(items.Count, Is.EqualTo(1), "the fallen player lying on the tag does not pick it up");
            Assert.That(items[0].ItemId, Is.EqualTo(1));
            Assert.That(items[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Tag));
            Assert.That(items[0].Team, Is.EqualTo(Team.Bravo));
            Assert.That(items[0].HasOwner, Is.True);
            Assert.That(items[0].OwnerId, Is.EqualTo(2));
            Assert.That(items[0].Position, Is.EqualTo(_field));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(items[0].HasCarrier, Is.False);
            Assert.That(Match().AlphaScore, Is.Zero, "a kill alone scores nothing");
            Assert.That(_match.AlphaScore, Is.Zero);
            Assert.That(Row(1).Kills, Is.EqualTo(1));

            Place(1, _field);
            Step();

            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed confirm tag=1 owner=2 side=1 by=1 by-side=0"));
            Assert.That(_log.ToString(), Does.Contain("[Match] score A=1 B=0"));
            Assert.That(Items().Count, Is.Zero);
            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.KillConfirmed));
            Assert.That(snapshot.AlphaScore, Is.EqualTo(1));
            Assert.That(snapshot.BravoScore, Is.Zero);
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(1));
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(_match.AlphaScore, Is.EqualTo(1));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));

            Advance(0.6f);
            Assert.That(_module.Session.IsAlive(2), Is.True);
            Place(2, _otherField);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Place(1, _otherField);
            Step();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.AlphaScore, Is.EqualTo(2));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(2));
            Assert.That(Items().Count, Is.Zero);
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\"").And.Contain("\"scoreAlpha\":\"2\""));
            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed state=Ended winner=Alpha score=2-0 confirmed=2-0 denied=0-0"));
        }

        [Test]
        public void AnAllyPickingUpTheTagDeniesItWithoutScoring()
        {
            Start(Spec(scoreLimit: 5));
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Place(3, _farAway);
            Step();

            Place(1, _field);
            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();
            Assert.That(Items().Single().Team, Is.EqualTo(Team.Alpha));

            Place(3, _field);
            Step();

            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed deny tag=1 owner=1 side=0 by=3 by-side=0"));
            Assert.That(Items().Count, Is.Zero);
            var rules = (KillConfirmedRules)_module.Objective;
            Assert.That(rules.KillConfirmed.GetDenied(Side.Alpha), Is.EqualTo(1));
            Assert.That(Match().AlphaScore, Is.Zero);
            Assert.That(Match().BravoScore, Is.Zero);
            Assert.That(Match().BravoPoints, Is.Zero);
            Assert.That(_match.BravoScore, Is.Zero);
        }

        [Test]
        public void ATagNobodyReachesExpires()
        {
            Start(Spec(scoreLimit: 5, tagLifetimeSeconds: 1f, respawnSeconds: 5f));
            Join(1);
            Join(2);
            Step();

            Place(2, _field);
            _module.ApplyDamage(1, 2, 100);
            Step();
            Assert.That(Items().Count, Is.EqualTo(1));

            Advance(1.1f);

            Assert.That(Items().Count, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed expire tag=1 owner=2 side=1"));
            Place(1, _field);
            Step();
            Assert.That(Match().AlphaScore, Is.Zero, "an expired tag cannot be confirmed");
        }

        [Test]
        public void TheLeaderByConfirmsWinsAtTimeUp()
        {
            Start(Spec(scoreLimit: 10, timeLimitSeconds: 2f));
            Join(1);
            Join(2);
            Step();
            Assert.That(Match().RemainingSeconds, Is.EqualTo(2));

            Place(2, _field);
            _module.ApplyDamage(1, 2, 100);
            Place(1, _field);
            Step();
            Assert.That(Match().AlphaScore, Is.EqualTo(1));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void AnEvenTimeUpIsADraw()
        {
            Start(Spec(scoreLimit: 10, timeLimitSeconds: 1f));
            Join(1);
            Join(2);
            Step();

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().HasWinner, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed state=Ended winner=draw score=0-0 confirmed=0-0 denied=0-0"));
        }

        [Test]
        public void TheNextMatchStartsFromNoConfirmsAndNoTags()
        {
            Start(Spec(scoreLimit: 1), restartDelaySeconds: 2f);
            Join(1);
            Join(2);
            Step();
            Place(2, _field);
            _module.ApplyDamage(1, 2, 100);
            Place(1, _field);
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.AlphaScore, Is.Zero);
            var snapshot = Match();
            Assert.That(snapshot.AlphaScore, Is.Zero);
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.HasWinner, Is.False);
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(60));
            Assert.That(Items().Count, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Mode] killconfirmed restart"));
        }

        [Test]
        public void TheSnapshotCarriesTheTags()
        {
            Start(Spec(scoreLimit: 5));
            Join(1);
            Join(2);
            Step();
            Place(2, _field);
            _module.ApplyDamage(1, 2, 100);
            Step();

            var snapshot = new SnapshotMessage();
            _module.Contribute(snapshot, null);

            Assert.That(snapshot.ObjectiveItems.Count, Is.EqualTo(1));
            Assert.That(snapshot.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Tag));
            Assert.That(snapshot.ObjectiveItems[0].OwnerId, Is.EqualTo(2));
            Assert.That(snapshot.Match.Objective, Is.EqualTo(MatchObjective.KillConfirmed));
        }

        [Test]
        public void ATagReachesABodyWithinThePickupRadiusOfItsCapsule()
        {
            var bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            var body = bodies.Add(1);
            body.SetPose(Vector3.Zero, 0f, PlayerStance.Standing);
            var edge = 1f + body.Radius;

            var middle = body.Center.Y;

            Assert.That(KillConfirmedRules.Reaches(body, new Vector3(edge - 0.01f, middle, 0f), 1f), Is.True);
            Assert.That(KillConfirmedRules.Reaches(body, new Vector3(edge + 0.01f, middle, 0f), 1f), Is.False);
            Assert.That(KillConfirmedRules.Reaches(body, Vector3.Zero, 1f), Is.True);
        }

        [Test]
        public void TheFactoryDoesNotPickKillConfirmedYet()
        {
            Assert.Throws<InvalidOperationException>(() => Start(Spec(scoreLimit: 5), useFactory: true));
        }

        private static KillConfirmedSpec Spec(
            int scoreLimit, float timeLimitSeconds = 60f, float respawnSeconds = 0.5f, float tagLifetimeSeconds = 30f)
        {
            return new KillConfirmedSpec(scoreLimit, timeLimitSeconds, respawnSeconds, tagLifetimeSeconds, 1, 1f);
        }

        private static GameRules Rules()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithRespawnMode(RespawnMode.Individual)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(MatchObjective.KillConfirmed);
        }

        private void Start(KillConfirmedSpec spec, float restartDelaySeconds = 10f, bool useFactory = false)
        {
            var settings = new NetMatchSettings(restartDelaySeconds: restartDelaySeconds, killConfirmed: spec);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(Rules(), settings));
            var module = useFactory
                ? new MatchRulesModule()
                : new MatchRulesModule((objective, s, host) => objective == MatchObjective.KillConfirmed
                    ? new KillConfirmedRules(s, host)
                    : ServerObjectiveFactory.Create(objective, s, host));
            module.Start(_context);
            _module = module;
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Place(int id, Vector3 position)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            body.SetPose(position, 0f, PlayerStance.Standing);
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

        private List<ObjectiveItemSnapshot> Items()
        {
            var snapshot = new SnapshotMessage();
            _module.FillObjectiveItems(snapshot.ObjectiveItems);
            var items = new List<ObjectiveItemSnapshot>();
            for (var i = 0; i < snapshot.ObjectiveItems.Count; i++)
            {
                items.Add(snapshot.ObjectiveItems[i]);
            }

            return items;
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
