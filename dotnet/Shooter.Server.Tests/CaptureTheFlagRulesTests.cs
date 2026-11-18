using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class CaptureTheFlagRulesTests
    {
        private static readonly Vector3 _alphaBase = new Vector3(-20f, 1f, 0f);
        private static readonly Vector3 _bravoBase = new Vector3(20f, 1f, 0f);

        private StringWriter _log;
        private FakeHost _host;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _host = new FakeHost(_log);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void TheSessionHasNoLimitsAndRespawnsWithTheCaptureTheFlagTime()
        {
            var settings = new NetMatchSettings(
                scoreLimit: 30,
                timeLimitSeconds: 600f,
                respawnSeconds: 9f,
                captureTheFlag: new CaptureTheFlagSpec(scoreLimit: 4, timeLimitSeconds: 300f, respawnSeconds: 2.5f));

            var rules = new CaptureTheFlagRules(settings, _host);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.CaptureTheFlag));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.RemainingSeconds, Is.EqualTo(300f));
            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.RespawnsByRound, Is.False);
        }

        [Test]
        public void TheGameReadsTheConfiguredValuesAndLeavesRespawningToTheSession()
        {
            var configured = new CaptureTheFlagSpec(
                scoreLimit: 5, timeLimitSeconds: 400f, respawnSeconds: 3f, pickupRadius: 1.2f, captureRadius: 2.5f, returnSeconds: 12f, pointsPerCapture: 2);

            var rules = new CaptureTheFlagRules(new NetMatchSettings(captureTheFlag: configured), _host);

            var spec = rules.Game.Spec;
            Assert.That(spec.ScoreLimit, Is.EqualTo(5));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(400f));
            Assert.That(spec.RespawnSeconds, Is.Zero);
            Assert.That(spec.PickupRadius, Is.EqualTo(1.2f));
            Assert.That(spec.CaptureRadius, Is.EqualTo(2.5f));
            Assert.That(spec.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(spec.PointsPerCapture, Is.EqualTo(2));
        }

        [Test]
        public void TheFlagsStartAtTheirBasesAndAreLoggedWhenStarted()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);
            var items = new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
            rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.Zero, "no map, no flags");

            rules.UseMap(TestMaps.Data("Flags", Points()), Points());
            rules.Start();

            Assert.That(_log.ToString(), Does.Contain(
                "[Mode] ctf net scoreLimit=3 timeLimit=600s respawn=5s returnTime=20s pickupRadius=1.5 captureRadius=2 pointsPerCapture=1"));
            Assert.That(_log.ToString(), Does.Contain("[Mode] flags 0=AtBase 1=AtBase"));
            rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].ItemId, Is.EqualTo(0));
            Assert.That(items[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Flag));
            Assert.That(items[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(items[0].HasOwner, Is.False);
            Assert.That(items[0].Position, Is.EqualTo(_alphaBase));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(items[0].HasCarrier, Is.False);
            Assert.That(items[1].ItemId, Is.EqualTo(1));
            Assert.That(items[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(items[1].Position, Is.EqualTo(_bravoBase));

            var snapshot = new MatchSnapshot { AlphaPoints = 4, BravoPoints = 2, ObjectiveRemainingSeconds = 9 };
            rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.CaptureTheFlag));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.Zero, "no flag is down");
            Assert.That(rules.DecideWinner(Team.Alpha), Is.Null, "no captures is a draw whoever has more kills");
            rules.Stop();
        }

        [Test]
        public void TheMatchRestartsFromNoCaptures()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);

            rules.MatchRestarted();

            Assert.That(_host.Scores, Is.EqualTo((0, 0)));
            Assert.That(rules.Game.Session.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void NothingHappensWhileTheMatchIsNotPlaying()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);
            rules.UseMap(TestMaps.Data("Flags", Points()), Points());

            Assert.That(rules.Tick(false, 1f), Is.False);
            Assert.That(rules.Game.Session.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void StartNeedsAMap()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);

            var e = Assert.Throws<InvalidOperationException>(() => rules.Start());

            Assert.That(e.Message, Is.EqualTo("the CaptureTheFlag rules have no map; call UseMap before Start"));
        }

        [Test]
        public void AMapMissingAFlagBaseIsRefused()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);
            var points = new NetMapPoints(
                TestRules.Points().SpawnPoints, Array.Empty<ControlPointArea>(), new[] { new FlagBasePoint(Team.Alpha, _alphaBase) });
            var map = TestMaps.Data("OneBase", points);

            var check = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, points));
            Assert.That(check.Message, Is.EqualTo("cannot change to map OneBase for the CaptureTheFlag match: it has no flag base for team Bravo"));
            var use = Assert.Throws<InvalidOperationException>(() => rules.UseMap(map, points));
            Assert.That(use.Message, Is.EqualTo("cannot start the CaptureTheFlag match: map OneBase has no flag base for team Bravo"));
        }

        [Test]
        public void AMapWithTwoBasesForATeamIsRefused()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);
            var points = new NetMapPoints(
                TestRules.Points().SpawnPoints,
                Array.Empty<ControlPointArea>(),
                new[] { new FlagBasePoint(Team.Alpha, _alphaBase), new FlagBasePoint(Team.Bravo, _bravoBase), new FlagBasePoint(Team.Alpha, Vector3.Zero) });
            var map = TestMaps.Data("TwoAlpha", points);

            var check = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, points));

            Assert.That(check.Message, Is.EqualTo("cannot change to map TwoAlpha for the CaptureTheFlag match: it has more than one flag base for team Alpha"));
        }

        [Test]
        public void AMapWithBothBasesPassesTheCheck()
        {
            var rules = new CaptureTheFlagRules(new NetMatchSettings(), _host);

            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Flags", Points()), Points()));
        }

        [TestCase(FlagState.AtBase, ObjectiveItemState.Home)]
        [TestCase(FlagState.Carried, ObjectiveItemState.Carried)]
        [TestCase(FlagState.Dropped, ObjectiveItemState.Dropped)]
        public void FlagStatesMapToObjectiveItemStates(FlagState state, ObjectiveItemState expected)
        {
            Assert.That(CaptureTheFlagRules.ToItemState(state), Is.EqualTo(expected));
        }

        [Test]
        public void TheRulesNeedSettingsAndAHost()
        {
            Assert.Throws<ArgumentNullException>(() => new CaptureTheFlagRules(null, _host));
            Assert.Throws<ArgumentNullException>(() => new CaptureTheFlagRules(new NetMatchSettings(), null));
        }

        private static NetMapPoints Points()
        {
            return new NetMapPoints(
                TestRules.Points().SpawnPoints,
                Array.Empty<ControlPointArea>(),
                new[] { new FlagBasePoint(Team.Alpha, _alphaBase), new FlagBasePoint(Team.Bravo, _bravoBase) });
        }

        // 試合を通さずに目的だけを確かめるので、Session と体は持たない
        private sealed class FakeHost : IMatchObjectiveHost
        {
            public FakeHost(TextWriter log)
            {
                Log = log;
            }

            public TeamDeathmatchSession Session => null;

            public ServerBodies Bodies => null;

            public TextWriter Log { get; }

            public (int Alpha, int Bravo)? Scores { get; private set; }

            public void SetMatchScores(int alpha, int bravo)
            {
                Scores = (alpha, bravo);
            }

            public void ClearDowned()
            {
            }
        }
    }
}
