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
    // 移動・ルール・戦闘・ストリークを同じホストで動かし、倒した数・UAV の印・空爆のダメージを確かめる
    public sealed class ServerStreaksTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const float _uavSeconds = 20f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        // 床 (y=0.5 が上面) に足を付けた体の原点
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerStreaks _streaks;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void TheUavBecomesReadyAtTheConfiguredNumberOfKills()
        {
            Start(Rules(), Spec(uavKills: 2, airstrikeKills: 3));
            JoinTwoTeams(6);

            Kill(1, 2);

            var afterOne = _streaks.StatusOf(1).Value;
            Assert.That(afterOne.Kills, Is.EqualTo(1));
            Assert.That(afterOne.ReadyCount, Is.EqualTo(0));
            Assert.That(Use(1, StreakSpec.UavRewardId), Is.EqualTo(ServerStreakRefusal.NothingReady));
            Assert.That(_log.ToString(), Does.Contain(StreakLog.NothingToUse("net1")));

            Kill(1, 4);

            var afterTwo = _streaks.StatusOf(1).Value;
            Assert.That(afterTwo.Kills, Is.EqualTo(2));
            Assert.That(afterTwo.ReadyCount, Is.EqualTo(1));
            Assert.That(afterTwo.Slots[0].RewardId, Is.EqualTo(StreakSpec.UavRewardId));
            Assert.That(afterTwo.Slots[0].Ready, Is.True);
            Assert.That(afterTwo.Slots[1].Ready, Is.False);
            Assert.That(_log.ToString(), Does.Contain(StreakLog.Earned("net1", StreakSpec.UavRewardId, afterTwo)));
        }

        [Test]
        public void UsingTheUavSendsWhereTheEnemiesAreToAlliesOnly()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);

            Assert.That(Use(1, StreakSpec.UavRewardId), Is.EqualTo(ServerStreakRefusal.None));

            var enemy = Body(4).Center;
            foreach (var ally in new[] { 1, 3 })
            {
                var placed = _transport.Read<PingPlacedMessage>(ally, Copy).Single();
                Assert.That(placed.PingId, Is.GreaterThanOrEqualTo(ServerStreaks.FirstMarkId));
                Assert.That(placed.Kind, Is.EqualTo(PingKind.Enemy));
                Assert.That(placed.Position, Is.EqualTo(enemy), "the living enemy is marked; the fallen one is not");
                Assert.That(placed.Tracking, Is.True);
                Assert.That(placed.RemainingSeconds, Is.EqualTo(_uavSeconds));
            }

            foreach (var enemyId in new[] { 2, 4 })
            {
                Assert.That(_transport.Read<PingPlacedMessage>(enemyId, Copy), Is.Empty, "enemies are not told where they are");
            }

            foreach (var everyone in new[] { 1, 2, 3, 4 })
            {
                var activated = _transport.Read<StreakActivatedMessage>(everyone, Copy).Single();
                Assert.That(activated.PlayerId, Is.EqualTo(1));
                Assert.That(activated.Team, Is.EqualTo(Team.Alpha));
                Assert.That(activated.RewardId, Is.EqualTo(StreakSpec.UavRewardId));
                Assert.That(activated.HasTarget, Is.False);
                Assert.That(activated.DurationSeconds, Is.EqualTo(_uavSeconds));
            }

            Assert.That(_streaks.StatusOf(1).Value.ReadyCount, Is.EqualTo(0), "the used reward leaves its slot");
            Assert.That(_streaks.MarkCount, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain(ServerStreakLog.UavMarked(1, Team.Alpha, 1, 2, _uavSeconds)));
        }

        [Test]
        public void TheUavMarkFollowsTheEnemyAndIsRemovedWhenTheyFall()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);
            Use(1, StreakSpec.UavRewardId);
            var markId = _transport.Read<PingPlacedMessage>(3, Copy).Single().PingId;

            Step(0.2f);

            var moved = _transport.Read<PingMovedMessage>(3, m => new PingMovedMessage { PingId = m.PingId, Position = m.Position });
            Assert.That(moved, Is.Not.Empty);
            Assert.That(moved[moved.Count - 1].PingId, Is.EqualTo(markId));
            Assert.That(Vector3.Distance(moved[moved.Count - 1].Position, Body(4).Center), Is.LessThan(0.01f), "the standing enemy stays where the mark is");
            Assert.That(_transport.Read<PingMovedMessage>(4, m => m), Is.Empty);

            Kill(3, 4);
            Step(0.2f);

            var removed = _transport.Read<PingRemovedMessage>(3, m => new PingRemovedMessage { PingId = m.PingId, Reason = m.Reason }).Single();
            Assert.That(removed.PingId, Is.EqualTo(markId));
            Assert.That(removed.Reason, Is.EqualTo(PingRemoval.TargetGone));
            Assert.That(_streaks.MarkCount, Is.EqualTo(0));
        }

        [Test]
        public void TheUavMarksOnlyEnemiesWithinItsRange()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3, uavRange: 5f));
            JoinTwoTeams(4);
            Kill(1, 2);

            Assert.That(Use(1, StreakSpec.UavRewardId), Is.EqualTo(ServerStreakRefusal.None));

            Assert.That(_transport.Read<PingPlacedMessage>(3, Copy), Is.Empty, "the enemy is 10 m away");
            Assert.That(_log.ToString(), Does.Contain(ServerStreakLog.UavMarked(1, Team.Alpha, 0, 2, _uavSeconds)));
        }

        [Test]
        public void AnAllyWhoJoinsDuringTheUavIsShownItsMarks()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);
            Use(1, StreakSpec.UavRewardId);

            Join(5);

            var placed = _transport.Read<PingPlacedMessage>(5, Copy);
            Assert.That(placed, Has.Count.EqualTo(_rules.Session.GetTeam(5) == Team.Alpha ? 1 : 0));
        }

        [Test]
        public void TheAirstrikeFallsWhereTheUserAimsAndDamagesThroughCombat()
        {
            Start(Rules(), Spec(uavKills: 3, airstrikeKills: 1, shellCount: 1, intervalSeconds: 0.1f, spread: 0f));
            JoinTwoTeams(4);
            Kill(1, 2);
            Assert.That(_streaks.StatusOf(1).Value.Slots[1].Ready, Is.True);
            var target = new Vector3(Body(4).Position.X, 0.5f, Body(4).Position.Z);

            Assert.That(Use(1, StreakSpec.AirstrikeRewardId, target - Body(1).EyePosition), Is.EqualTo(ServerStreakRefusal.None));

            var activated = _transport.Read<StreakActivatedMessage>(2, Copy).Single();
            Assert.That(activated.RewardId, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(activated.HasTarget, Is.True);
            Assert.That(Vector3.Distance(activated.Target, target), Is.LessThan(0.01f), "the server finds the floor where the user aimed");
            Assert.That(activated.DurationSeconds, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(_rules.Session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the shell has not landed yet");
            Assert.That(_streaks.PendingShellCount, Is.EqualTo(1));

            Step(0.2f);

            Assert.That(_rules.Session.IsAlive(4), Is.False, _log.ToString());
            var killed = _transport.Read<KilledMessage>(4, m => new KilledMessage { KillerId = m.KillerId, VictimId = m.VictimId, WeaponId = m.WeaponId }).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.WeaponId, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(_transport.Read<ExplosionMessage>(3, m => new ExplosionMessage { WeaponId = m.WeaponId }).Single().WeaponId, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the user is 10 m away");
            Assert.That(_streaks.StatusOf(1).Value.Kills, Is.EqualTo(2), "an airstrike kill counts toward the next streak");
            Assert.That(_log.ToString(), Does.Contain(ServerStreakLog.AirstrikeShell(1, 1, 1, 1, 1)));
        }

        [Test]
        public void AnAirstrikeWithoutAnAimIsRefusedAndKeepsTheReward()
        {
            Start(Rules(), Spec(uavKills: 3, airstrikeKills: 1));
            JoinTwoTeams(4);
            Kill(1, 2);

            Assert.That(Use(1, StreakSpec.AirstrikeRewardId, Vector3.Zero), Is.EqualTo(ServerStreakRefusal.InvalidAim));
            Assert.That(Use(1, StreakSpec.AirstrikeRewardId, new Vector3(float.NaN, 0f, 1f)), Is.EqualTo(ServerStreakRefusal.InvalidAim));

            Assert.That(_streaks.StatusOf(1).Value.ReadyCount, Is.EqualTo(1));
            Assert.That(_streaks.PendingShellCount, Is.EqualTo(0));
            Assert.That(_transport.Read<StreakActivatedMessage>(1, Copy), Is.Empty);
        }

        [Test]
        public void ARequestForAnotherRewardThanTheNextIsRefused()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);

            Assert.That(Use(1, StreakSpec.AirstrikeRewardId), Is.EqualTo(ServerStreakRefusal.NotNext));

            Assert.That(_streaks.StatusOf(1).Value.ReadyCount, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain(ServerStreakLog.Refused(1, StreakSpec.AirstrikeRewardId, nameof(ServerStreakRefusal.NotNext))));
        }

        [Test]
        public void DyingResetsTheCountButKeepsTheEarnedRewardAndAFallenUserCannotUseIt()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);

            Kill(4, 1);

            var status = _streaks.StatusOf(1).Value;
            Assert.That(status.Kills, Is.EqualTo(0));
            Assert.That(status.Slots[0].Ready, Is.True);
            Assert.That(_log.ToString(), Does.Contain(StreakLog.Died("net1", 1, status)));
            Assert.That(Use(1, StreakSpec.UavRewardId), Is.EqualTo(ServerStreakRefusal.CannotAct));
            Assert.That(_streaks.StatusOf(1).Value.ReadyCount, Is.EqualTo(1));
        }

        [Test]
        public void KillingYourselfOrATeammateIsNotCounted()
        {
            Start(Rules().WithFriendlyFire(FriendlyFire.On), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);

            Kill(1, 3);
            Assert.That(_rules.Session.IsAlive(3), Is.False);
            Kill(1, 1);

            Assert.That(_streaks.StatusOf(1).Value.Kills, Is.EqualTo(0));
            Assert.That(_streaks.StatusOf(1).Value.ReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void WithStreaksOffNothingIsCountedOrUsed()
        {
            Start(Rules().WithStreaks(Streaks.Off), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);

            Assert.That(_streaks.IsEnabled, Is.False);
            Assert.That(_streaks.StatusOf(1).Value.Kills, Is.EqualTo(0));
            Assert.That(Use(1, StreakSpec.UavRewardId), Is.EqualTo(ServerStreakRefusal.RuleOff));
            Assert.That(_log.ToString(), Does.Contain(ServerStreakLog.Rule(Streaks.Off, _streaks.Spec)));
        }

        [Test]
        public void TheSnapshotCarriesEachPlayersStreakKills()
        {
            Start(Rules(), Spec(uavKills: 2, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);

            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(3, out var receiver), Is.True);
            _streaks.Contribute(snapshot, receiver);

            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var row = snapshot.Players[i];
                Assert.That(row.StreakKills, Is.EqualTo(row.PlayerId == 1 ? 1 : 0));
            }
        }

        [Test]
        public void TheRequestMessageGoesThroughTheHost()
        {
            Start(Rules(), Spec(uavKills: 1, airstrikeKills: 3));
            JoinTwoTeams(4);
            Kill(1, 2);

            _host.OnMessage(new ConnectionId(1), new StreakUseRequestMessage { RewardId = StreakSpec.UavRewardId, Direction = Vector3.UnitX });

            Assert.That(_transport.Read<StreakActivatedMessage>(3, Copy), Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain(StreakLog.Used("net1", StreakSpec.UavRewardId, _streaks.StatusOf(1).Value)));
        }

        [Test]
        public void RefusesMissingModules()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerStreaks(null, new ServerCombat(new MatchRulesModule(), new ServerMovement(ServerMotorSettings.Default))));
            Assert.Throws<ArgumentNullException>(() => new ServerStreaks(new MatchRulesModule(), null));
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private static StreakSpec Spec(
            int uavKills, int airstrikeKills, float uavRange = StreakUavSpec.DefaultRange, int shellCount = 1, float intervalSeconds = 0.1f, float spread = 0f)
        {
            return new StreakSpec(
                new[]
                {
                    new StreakReward(StreakSpec.UavRewardId, StreakSpec.DefaultUavNameKey, uavKills),
                    new StreakReward(StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, airstrikeKills),
                },
                new StreakUavSpec(_uavSeconds, uavRange),
                new StreakAirstrikeSpec(shellCount, intervalSeconds, spread));
        }

        private static PingPlacedMessage Copy(PingPlacedMessage m)
        {
            return new PingPlacedMessage { PingId = m.PingId, Kind = m.Kind, Position = m.Position, Tracking = m.Tracking, RemainingSeconds = m.RemainingSeconds };
        }

        private static StreakActivatedMessage Copy(StreakActivatedMessage m)
        {
            return new StreakActivatedMessage
            {
                PlayerId = m.PlayerId,
                Team = m.Team,
                RewardId = m.RewardId,
                HasTarget = m.HasTarget,
                Target = m.Target,
                DurationSeconds = m.DurationSeconds,
            };
        }

        private void Start(GameRules rules, StreakSpec spec)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, movement);
            _streaks = new ServerStreaks(_rules, _combat, spec, new Random(1165));
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_streaks);
            _host.Start(_context);
        }

        // 1, 3, 5 が Alpha、2, 4, 6 が Bravo に分かれる
        private void JoinTwoTeams(int count)
        {
            for (var id = 1; id <= count; id++)
            {
                Join(id);
            }

            Step(0.2f);
            for (var id = 1; id <= count; id++)
            {
                Assert.That(_rules.Session.GetTeam(id), Is.EqualTo(id % 2 == 1 ? Team.Alpha : Team.Bravo));
            }
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Kill(int killer, int victim)
        {
            _combat.DealDamage(killer, victim, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
        }

        private ServerStreakRefusal Use(int playerId, string rewardId, Vector3? direction = null)
        {
            return _streaks.Use(playerId, rewardId, direction ?? Vector3.UnitX);
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }
    }
}
