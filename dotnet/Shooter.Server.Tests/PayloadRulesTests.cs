using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class PayloadRulesTests
    {
        // TestRules.PayloadPoints の道筋は z = -10 から z = 10 の 20m で、中継点は z = 0 (10m)。体の高さ 1 は範囲の高さ 4 の内側
        private static readonly Vector3 _away = new Vector3(30f, 1f, 30f);

        private static readonly float[] _speedByPushers = { 1f, 2f };

        private StringWriter _log;
        private MatchHost _host;
        private PayloadRules _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _rules?.Stop();
            _log.Dispose();
        }

        [Test]
        public void TheObjectiveDecidesByTheCartAndKeepsTheSessionFromEndingFirst()
        {
            var settings = Settings(respawnSeconds: 7f);
            var rules = new PayloadRules(settings, new MatchHost(_log, TestRules.PayloadPoints()));

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.False);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.Payload, Is.Null);
            Assert.That(rules.RemainingSeconds, Is.Null);
            Assert.That(rules.CartPosition, Is.Null);
            Assert.That(rules.BotRally, Is.Null);
            Assert.That(rules.DecideWinner(Team.Bravo), Is.EqualTo(Team.Bravo), "without a map the kill winner stands");
            Assert.That(rules.Tick(true, 1f), Is.False);
            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(7f));
            Assert.That(spec.RespawnsByRound, Is.False);

            var snapshot = new MatchSnapshot
            {
                AlphaPoints = 4,
                HasActivePoint = true,
                ActivePointIndex = 2,
                ObjectiveRemainingSeconds = 9,
                PayloadProgress = 0.5f,
                PayloadCheckpointsPassed = 2,
                PayloadPushers = 3,
            };
            rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.HasActivePoint, Is.False);
            Assert.That(snapshot.ActivePointIndex, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.Zero);
            Assert.That(snapshot.PayloadProgress, Is.Zero);
            Assert.That(snapshot.PayloadCheckpointsPassed, Is.Zero);
            Assert.That(snapshot.PayloadPushers, Is.Zero);
            var items = Items();
            rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.Zero);
        }

        [Test]
        public void TheMapBuildsTheCartFromTheConfiguredTuningAndLogsWhenStarted()
        {
            var points = TestRules.PayloadPoints();
            var rules = new PayloadRules(Settings(), new MatchHost(_log, points));

            rules.UseMap(TestMaps.Data("NetArena01", points), points);
            rules.Start();
            _rules = rules;

            var payload = rules.Payload;
            Assert.That(payload.Spec.CartSpeed, Is.EqualTo(5f));
            Assert.That(payload.Attackers, Is.EqualTo(Side.Alpha));
            Assert.That(payload.Defenders, Is.EqualTo(Side.Bravo));
            Assert.That(payload.Route, Is.SameAs(points.Payload.Route));
            Assert.That(_log.ToString(), Does.Contain(
                "[Payload] net attackers=Alpha length=20.0m checkpoints=1 radius=3 cartSpeed=5 minPushers=1 rollbackDelay=1 rollbackSpeed=2 checkpointBonus=10 timeLimit=30"));
            Assert.That(rules.RemainingSeconds, Is.EqualTo(30f));
            Assert.That(rules.CartPosition, Is.EqualTo(new Vector3(0f, 0f, -10f)));
            var rally = rules.BotRally;
            Assert.That(rally.HasValue, Is.True);
            Assert.That(rally.Value.Center, Is.EqualTo(new Vector3(0f, 0f, -10f)));
            Assert.That(rally.Value.Radius, Is.EqualTo(3f));

            var items = Items();
            rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].ItemId, Is.EqualTo(PayloadRules.CartItemId));
            Assert.That(items[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Cart));
            Assert.That(items[0].Team, Is.EqualTo(Team.Alpha), "the cart belongs to the side that pushes it");
            Assert.That(items[0].Position, Is.EqualTo(new Vector3(0f, 0f, -10f)));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(items[0].HasOwner, Is.False);
            Assert.That(items[0].HasCarrier, Is.False);
            var controlPoints = new SnapshotList<ControlPointSnapshot>(SnapshotMessage.MaxControlPoints, "control point");
            rules.FillControlPoints(controlPoints);
            Assert.That(controlPoints.Count, Is.Zero);
        }

        [Test]
        public void TheAttackersComeFromTheMap()
        {
            var points = TestRules.PayloadPoints(Team.Bravo);
            var rules = new PayloadRules(Settings(), new MatchHost(_log, points));

            rules.UseMap(TestMaps.Data("Reversed", points), points);

            Assert.That(rules.Payload.Attackers, Is.EqualTo(Side.Bravo));
            Assert.That(rules.Payload.Defenders, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void AMapWithoutARouteIsRefusedWithTheReason()
        {
            var rules = new PayloadRules(Settings(), new MatchHost(_log, TestRules.Points()));
            var map = TestMaps.Data("Empty", TestRules.Points(controlPoints: 3));

            var check = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, TestRules.Points(controlPoints: 3)));
            Assert.That(check.Message, Is.EqualTo(
                "cannot change to map Empty for the Payload match: it has no payload route; "
                + "place a PayloadCart with its route in the scene and export the map points with make export-server-rules"));
            var use = Assert.Throws<InvalidOperationException>(() => rules.UseMap(map, TestRules.Points(controlPoints: 3)));
            Assert.That(use.Message, Is.EqualTo(
                "cannot start the Payload match on map Empty: it has no payload route; "
                + "place a PayloadCart with its route in the scene and export the map points with make export-server-rules"));
            Assert.Throws<InvalidOperationException>(() => rules.Start(), "Start needs a map");
            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Routed", TestRules.PayloadPoints()), TestRules.PayloadPoints()));
        }

        // Alpha が 1 秒押し、Bravo が止め、Bravo が倒れてまた押し、中継点で時間が延び、終点で Alpha が勝つ
        [Test]
        public void ThePayloadIsPushedToTheEnd()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(2, _away);
            Assert.That(_host.Session.State, Is.EqualTo(GameModeState.Playing));

            _host.Follow(1);
            _host.Steps(60);

            Assert.That(_rules.Payload.Distance, Is.EqualTo(5d).Within(0.01));
            Assert.That(_log.ToString(), Does.Contain("[Payload] cart=pushing pushers=1 defenders=0 progress=0.00"));
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.PayloadProgress, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(snapshot.PayloadPushers, Is.EqualTo(1));
            Assert.That(snapshot.PayloadCheckpointsPassed, Is.Zero);
            var items = Items();
            _rules.FillObjectiveItems(items);
            Assert.That(items[0].Position.Z, Is.EqualTo(-5f).Within(0.01f), "the cart item rides along the route");

            _host.Follow(2);
            _host.Steps(30);

            Assert.That(_rules.Payload.Distance, Is.EqualTo(5d).Within(0.01), "a defender beside the cart stops it");
            Assert.That(_rules.Payload.IsContested, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Payload] cart=contested pushers=1 defenders=1 progress=0.25"));

            _host.Unfollow(2);
            Assert.That(_host.Session.ApplyDamage(1, 2, _host.Session.MaxHealth), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_rules.Payload.IsContested, Is.False, "the fallen are taken off the cart at once");
            _host.Steps(66);

            Assert.That(_rules.Payload.CheckpointsReached, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Payload] checkpoint=1/1 progress=0.50 bonus=10.0s remaining="));
            Assert.That(_rules.RemainingSeconds, Is.GreaterThan(30f), "the checkpoint adds 10 s to the 30 s limit");
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.PayloadCheckpointsPassed, Is.EqualTo(1));
            Assert.That(_host.Ended, Is.False);

            _host.Steps(130);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(_host.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.Payload.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.Payload.IsDelivered, Is.True);
            Assert.That(_rules.DecideWinner(Team.Bravo), Is.EqualTo(Team.Alpha), "the cart decides, not the kills");
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.PayloadProgress, Is.EqualTo(1f));
            Assert.That(_log.ToString(), Does.Contain("[Payload] state=Ended delivered=true progress=1.00 checkpoints=1/1 remaining="));
            Assert.That(_log.ToString(), Does.Contain(" winner=Alpha"));
        }

        [Test]
        public void TimeRunningOutGoesToTheDefenders()
        {
            Begin(Settings(timeLimitSeconds: 1f));
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(1, _away);
            _host.Place(2, _away);
            _host.Session.ApplyDamage(1, 2, _host.Session.MaxHealth);

            _host.Steps(70);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Bravo), "a kill lead does not help the attackers");
            Assert.That(_rules.RemainingSeconds, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Payload] state=Ended delivered=false progress=0.00 checkpoints=0/1 remaining=0.0s winner=Bravo"));
        }

        [Test]
        public void AMatchStoppedBeforeTheEndGoesToTheDefenders()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);

            Assert.That(_rules.DecideWinner(Team.Alpha), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void TheCartRollsBackAfterTheDelayButNotPastAReachedCheckpoint()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(2, _away);
            _host.Follow(1);
            _host.Steps(60);
            Assert.That(_rules.Payload.Distance, Is.EqualTo(5d).Within(0.01));

            _host.Unfollow(1);
            _host.Place(1, _away);
            _host.Steps(60);
            Assert.That(_rules.Payload.Distance, Is.EqualTo(5d).Within(0.01), "the cart waits the rollback delay");
            Assert.That(_log.ToString(), Does.Contain("[Payload] cart=idle pushers=0 defenders=0 progress=0.25"));

            _host.Steps(60);
            Assert.That(_rules.Payload.Distance, Is.EqualTo(3d).Within(0.05), "2 m/s back for 1 s");
            Assert.That(_log.ToString(), Does.Contain("[Payload] cart=rolling-back pushers=0 defenders=0"));

            _host.Follow(1);
            _host.Steps(144);
            Assert.That(_rules.Payload.CheckpointsReached, Is.EqualTo(1));
            _host.Unfollow(1);
            _host.Place(1, _away);
            _host.Steps(600);

            Assert.That(_rules.Payload.Distance, Is.EqualTo(10d).Within(1e-6), "a reached checkpoint is the floor");
            Assert.That(_host.Ended, Is.False);
        }

        [Test]
        public void TheFallenDoNotPush()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(2, _away);
            _host.Follow(1);
            _host.Steps(1);
            Assert.That(_rules.Payload.Pushers, Is.EqualTo(1));

            _host.Session.ApplyDamage(2, 1, _host.Session.MaxHealth);

            Assert.That(_rules.Payload.Pushers, Is.Zero, "taken off the cart at once");
            var before = _rules.Payload.Distance;
            _host.Steps(30);
            Assert.That(_rules.Payload.Distance, Is.EqualTo(before), "the dead body beside the cart does not push");
        }

        [Test]
        public void TheNextMatchStartsTheCartAgainAndFollowsTeamChanges()
        {
            Begin(Settings(timeLimitSeconds: 1f));
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(2, _away);
            _host.Follow(1);
            _host.Steps(70);
            Assert.That(_host.Ended, Is.True);
            Assert.That(_rules.Payload.Distance, Is.GreaterThan(0d));

            Assert.That(_host.Session.ChangeTeam(1, Team.Bravo), Is.True);
            _rules.PlayerJoined(1);
            Assert.That(_rules.Payload.Session.GetSide(1), Is.EqualTo(Side.Bravo));
            _host.Session.Restart();

            Assert.That(_log.ToString(), Does.Contain("[Payload] restart"));
            Assert.That(_rules.Payload.Distance, Is.Zero);
            Assert.That(_rules.Payload.CheckpointsReached, Is.Zero);
            Assert.That(_rules.Payload.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(_rules.CartPosition, Is.EqualTo(new Vector3(0f, 0f, -10f)));
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.PayloadProgress, Is.Zero);
        }

        [Test]
        public void APlayerWhoLeavesNoLongerPushes()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(2, _away);
            _host.Follow(1);
            _host.Steps(1);
            Assert.That(_rules.Payload.Pushers, Is.EqualTo(1));

            _rules.PlayerLeft(1);

            Assert.That(_rules.Payload.Session.HasPlayer(1), Is.False);
            Assert.That(_rules.Payload.Pushers, Is.Zero);
            Assert.DoesNotThrow(() => _host.Steps(1), "the body left behind is not counted");
        }

        [Test]
        public void ChangingTheMapRebuildsTheCartAndKeepsTheRulesStarted()
        {
            Begin(Settings());
            var other = new NetMapPoints(
                TestRules.Points().SpawnPoints,
                Array.Empty<ControlPointArea>(),
                null,
                new NetMapPayload(Team.Bravo, new[] { new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 30f) }, null, 2f, 3f));

            _rules.UseMap(TestMaps.Data("Other", other), other);

            Assert.That(_rules.Payload.Route.Length, Is.EqualTo(30d));
            Assert.That(_rules.Payload.Attackers, Is.EqualTo(Side.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[Payload] net attackers=Bravo length=30.0m checkpoints=0 radius=2"));
        }

        private static SnapshotList<ObjectiveItemSnapshot> Items()
        {
            return new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
        }

        private static NetMatchSettings Settings(float timeLimitSeconds = 30f, float respawnSeconds = 10f)
        {
            return new NetMatchSettings(
                respawnSeconds: respawnSeconds,
                payload: new PayloadSpec(5f, 1, _speedByPushers, 1f, 2f, 10f, timeLimitSeconds));
        }

        private void Begin(NetMatchSettings settings)
        {
            var points = TestRules.PayloadPoints();
            _host = new MatchHost(_log, points);
            _rules = new PayloadRules(settings, _host);
            _rules.UseMap(TestMaps.Data("NetArena01", points), points);
            _host.Begin(_rules, Plain(), settings);
            _rules.Start();
        }

        private static GameRules Plain()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        // 目的だけを確かめるので、MatchRulesModule が目的を回す順 (Session の Tick の後に目的の Tick、
        // 目的が終えたら DecideWinner の勝者で Session を Finish) と出来事の渡し方をここでなぞる (HardpointRulesTests と同じ)
        private sealed class MatchHost : IMatchObjectiveHost
        {
            private const float _tickSeconds = 1f / 60f;

            private readonly NetMapPoints _points;
            private readonly List<int> _following = new List<int>();
            private PayloadRules _rules;
            private long _tick;

            public MatchHost(TextWriter log, NetMapPoints points)
            {
                Log = log;
                _points = points;
                Bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            }

            public TeamDeathmatchSession Session { get; private set; }

            public ServerBodies Bodies { get; }

            public TextWriter Log { get; }

            public bool Ended { get; private set; }

            public Team? Winner { get; private set; }

            public void Begin(PayloadRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                Session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    _points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds);
                Session.Spawned += (playerId, point) => Bodies.Teleport(playerId, point.Position, 0f, _tick);
                Session.Killed += (killerId, victimId, _) => _rules.PlayerKilled(killerId, victimId);
                Session.Restarted += () =>
                {
                    Ended = false;
                    Winner = null;
                    _rules.MatchRestarted();
                };
                Session.Ended += winner =>
                {
                    Ended = true;
                    Winner = _rules.DecideWinner(winner);
                    _rules.MatchEnded();
                };
            }

            public void Join(int playerId, Team team)
            {
                Bodies.Add(playerId);
                Session.Join(playerId, "device" + playerId, team);
                _rules.PlayerJoined(playerId);
            }

            public void Place(int playerId, Vector3 position)
            {
                Assert.That(Bodies.TryGet(playerId, out var body), Is.True);
                body.SetPose(position, 0f, PlayerStance.Standing);
            }

            // 台車の横 (1m 脇、高さ 1m) に毎 tick 立たせ、台車と一緒に歩かせる
            public void Follow(int playerId)
            {
                if (!_following.Contains(playerId))
                {
                    _following.Add(playerId);
                }
            }

            public void Unfollow(int playerId)
            {
                _following.Remove(playerId);
            }

            public void Steps(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _tick++;
                    foreach (var playerId in _following)
                    {
                        Place(playerId, _rules.CartPosition.Value + new Vector3(1f, 1f, 0f));
                    }

                    Session.Tick(_tickSeconds);
                    if (_rules.Tick(Session.State == GameModeState.Playing, _tickSeconds))
                    {
                        Session.Finish(_rules.DecideWinner(Session.Winner));
                    }
                }
            }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }

            private Vector3 PositionOf(int playerId)
            {
                return Bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
            }
        }
    }
}
