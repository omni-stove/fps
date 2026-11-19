using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・Bot・スナップショットを MatchServer と同じ順で 1 つのホストに載せ、人の接続を模して Bot を動かす
    public sealed class ServerBotsTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
        private static readonly int[] _wallHealth = { 100 };
        private static readonly bool[] _wallGrounded = { true };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private StubControl _control;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerBots _bots;
        private ServerCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
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
        public void NoBotsAreAddedUntilAHumanJoins()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 4);
            Step(1f);

            Assert.That(_bots.Count, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Bot] fill target=4 bots=4 minPlayers=0"));
        }

        [Test]
        public void BotsFillUpToTheLaunchCountOnBothTeams()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 4);
            Join(1);
            Tick();

            Assert.That(_bots.Count, Is.EqualTo(3));
            Assert.That(_match.HumanCount, Is.EqualTo(1));
            Assert.That(_match.CountPlayers(Team.Alpha), Is.EqualTo(2));
            Assert.That(_match.CountPlayers(Team.Bravo), Is.EqualTo(2));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            foreach (var botId in _bots.BotIds)
            {
                Assert.That(BotRoster.IsBotId(botId), Is.True);
                Assert.That(_rules.Session.HasPlayer(botId), Is.True);
                Assert.That(_movement.TryGetState(botId, out _), Is.True);
                Assert.That(_context.Bodies.TryGet(botId, out var body) && body.HasSpawned, Is.True);
                Assert.That(_log.ToString(), Does.Contain("[Bot] added player=" + Text(botId) + " name=" + BotRoster.KillFeedName(botId)));
            }
        }

        [Test]
        public void MinPlayersFillsWithoutTheBotsFlag()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules().WithMinPlayers(2), bots: 0);
            Join(1);
            Tick();

            Assert.That(_bots.Count, Is.EqualTo(1));
        }

        [Test]
        public void AHumanReplacesABotOnTheTeamTheHumanJoined()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 4);
            Join(1);
            Tick();
            var before = _bots.BotIds.ToDictionary(id => id, TeamOf);
            Join(2);
            var team = TeamOf(2);
            Tick();

            var removed = before.Keys.Except(_bots.BotIds).Single();
            Assert.That(before[removed], Is.EqualTo(team));
            Assert.That(_bots.Count, Is.EqualTo(2));
            Assert.That(_match.CountPlayers(Team.Alpha), Is.EqualTo(2));
            Assert.That(_match.CountPlayers(Team.Bravo), Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain(" reason=replaced"));
        }

        [Test]
        public void BotsLeaveWhenTheLastHumanLeaves()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 3);
            Join(1);
            Tick();
            var botIds = _bots.BotIds.ToList();

            Leave(1);
            Tick();

            Assert.That(_bots.Count, Is.Zero);
            Assert.That(_match.PlayerCount, Is.Zero);
            foreach (var botId in botIds)
            {
                Assert.That(_rules.Session.HasPlayer(botId), Is.False);
                Assert.That(_movement.TryGetState(botId, out _), Is.False);
                Assert.That(_context.Bodies.TryGet(botId, out _), Is.False);
            }

            Assert.That(_log.ToString(), Does.Contain(" reason=no-humans"));
        }

        [Test]
        public void BotsAreNeitherIdleKickedNorSentAnything()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules().WithIdleKickSeconds(5f), bots: 2);
            Join(1);
            Step(1f);

            var botId = _bots.BotIds.Single();
            Assert.That(_rules.IdleTracker.Contains(botId), Is.False);
            Assert.That(_rules.IdleTracker.Contains(1), Is.True);
            Assert.That(_transport.Sent.Any(sent => !sent.Connection.IsValid), Is.False);
            Assert.That(_transport.Sent.Any(sent => sent.Connection.Value == 1), Is.True);
        }

        [Test]
        public void SnapshotsCarryTheBotsWhereTheyMoved()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 2);
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Assert.That(_context.Bodies.TryGet(botId, out var body), Is.True);
            var start = body.Position;
            Step(3f);

            Assert.That(Vector3.Distance(Flat(body.Position), Flat(start)), Is.GreaterThan(1f), _log.ToString());
            var snapshot = new SnapshotMessage();
            snapshot.Players.Add().PlayerId = botId;
            Assert.That(_match.TryGetPlayer(1, out var receiver), Is.True);
            _movement.Contribute(snapshot, receiver);
            Assert.That(snapshot.Players[0].Position, Is.EqualTo(body.Position));
            Assert.That(_log.ToString(), Does.Contain("[Bot] pos player=" + Text(botId)));
        }

        [Test]
        public void BotsShootEachOtherAndTheKillsAreLogged()
        {
            // 人は撃ち合いの外 (見える距離の外) に置き、Bot どうしで撃ち合わせる
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-28f, 1.1f, -28f), Team.Alpha),
                    new SpawnPoint(new Vector3(-6f, 1.1f, 20f), Team.Alpha),
                    new SpawnPoint(new Vector3(6f, 1.1f, 20f), Team.Bravo),
                },
                Array.Empty<ControlPointArea>());
            Start(BotTestMaps.Flat(points), Rules(), bots: 3, spec: SightOf(20f));
            Join(1);
            var kills = 0;
            for (var elapsed = 0f; elapsed < 60f && kills == 0; elapsed += _tickSeconds)
            {
                Tick();
                kills = _json.ToString().Split('\n').Count(line => line.Contains("\"type\":\"kill\"", StringComparison.Ordinal)
                    && line.Contains("\"killerId\":\"10000000", StringComparison.Ordinal));
            }

            Assert.That(kills, Is.GreaterThan(0), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=10000000"));
            Assert.That(_log.ToString(), Does.Match(@"\[Match\] kill .*1000000"));
        }

        [Test]
        public void BotsDoNotShootThroughWhatBlocksTheirSight()
        {
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-28f, 1.1f, -28f), Team.Alpha),
                    new SpawnPoint(new Vector3(-6f, 1.1f, 20f), Team.Alpha),
                    new SpawnPoint(new Vector3(6f, 1.1f, 20f), Team.Bravo),
                },
                Array.Empty<ControlPointArea>());
            Start(BotTestMaps.Flat(points), Rules(), bots: 3, spec: SightOf(20f));
            _combat.SightBlockers.Add(new BlockEverything());
            Join(1);
            for (var elapsed = 0f; elapsed < 20f; elapsed += _tickSeconds)
            {
                Tick();
            }

            Assert.That(_log.ToString(), Does.Not.Contain("[Net] hit attacker=10000000"));
        }

        [Test]
        public void BlindedBotsDoNotShoot()
        {
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-28f, 1.1f, -28f), Team.Alpha),
                    new SpawnPoint(new Vector3(-6f, 1.1f, 20f), Team.Alpha),
                    new SpawnPoint(new Vector3(6f, 1.1f, 20f), Team.Bravo),
                },
                Array.Empty<ControlPointArea>());
            Start(BotTestMaps.Flat(points), Rules(), bots: 3, spec: SightOf(20f));
            Join(1);
            for (var elapsed = 0f; elapsed < 20f; elapsed += _tickSeconds)
            {
                foreach (var body in _context.Bodies.All.ToList())
                {
                    if (body.PlayerId != 1 && _combat.StatusEffects.RemainingSeconds(body.PlayerId, StatusEffectKinds.Blind) <= 0f)
                    {
                        _combat.StatusEffects.Afflict(null, ServerCombat.GrenadeWeaponId, body.PlayerId, BlindEffect.StatusEffect(2f));
                    }
                }

                Tick();
            }

            Assert.That(_log.ToString(), Does.Not.Contain("[Net] hit attacker=10000000"));
        }

        [Test]
        public void BotsDoNotShootThroughSmokeUntilItClears()
        {
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-28f, 1.1f, -28f), Team.Alpha),
                    new SpawnPoint(new Vector3(-6f, 1.1f, 20f), Team.Alpha),
                    new SpawnPoint(new Vector3(6f, 1.1f, 20f), Team.Bravo),
                },
                Array.Empty<ControlPointArea>());
            Start(BotTestMaps.Flat(points), Rules(), bots: 3, spec: SightOf(20f));
            Join(1);
            Tick();

            // Bot が煙を回り込んで撃たないよう、床をまるごと煙で覆う
            const float smokeSeconds = 20f;
            var smoke = new AreaEffectSpec(60f, smokeSeconds, 0.5f, 0, visualId: "smoke");
            _combat.AreaEffects.Spread(smoke, new Vector3(0f, 1.1f, 0f), null, string.Empty);
            Step(smokeSeconds - 1f);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] hit attacker=10000000"));

            var hit = false;
            for (var elapsed = 0f; elapsed < 60f && !hit; elapsed += _tickSeconds)
            {
                Tick();
                hit = _log.ToString().Contains("[Net] hit attacker=10000000", StringComparison.Ordinal);
            }

            Assert.That(_combat.AreaEffects.Count, Is.Zero);
            Assert.That(hit, Is.True, _log.ToString());
        }

        [Test]
        public void ABotClimbsDownAndUpTheLadderToReachItsPatrolPoints()
        {
            // 見える距離を縮めて人を目に入れず、出現地点 (足場の上と下) を巡回させる
            Start(BotTestMaps.Ladder(BotTestMaps.Points(BotTestMaps.LowerSpawn, BotTestMaps.UpperSpawn)), Rules(), bots: 2, spec: SightOf(1f));
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Assert.That(TeamOf(botId), Is.EqualTo(Team.Bravo));

            // 人は巡回の道筋から離れた床の隅に置き、Bot の目に入れない
            _context.Bodies.Teleport(1, new Vector3(9f, 1.1f, -9f), 0f, _host.TickCount);
            Assert.That(_context.Bodies.TryGet(botId, out var body), Is.True);
            Assert.That(body.Position.Y, Is.GreaterThan(BotTestMaps.PlatformY));

            var reachedBottom = false;
            var reachedTopAgain = false;
            for (var elapsed = 0f; elapsed < 90f && !reachedTopAgain; elapsed += _tickSeconds)
            {
                Tick();
                if (!reachedBottom && Near(body.Position, BotTestMaps.LowerSpawn))
                {
                    reachedBottom = true;
                }
                else if (reachedBottom && Near(body.Position, BotTestMaps.UpperSpawn))
                {
                    reachedTopAgain = true;
                }
            }

            var log = _log.ToString();
            Assert.That(reachedBottom, Is.True, log);
            Assert.That(reachedTopAgain, Is.True, log);
            Assert.That(log, Does.Contain("[Bot] ladder player=" + Text(botId) + " ladder=0 climb=Down"));
            Assert.That(log, Does.Contain("[Bot] ladder player=" + Text(botId) + " ladder=0 climb=Up"));
            Assert.That(log, Does.Contain("[Bot] ladder player=" + Text(botId) + " ladder=0 climb=Done"));
            Assert.That(log, Does.Contain("[Net] ladder owner=" + Text(botId)));
            Assert.That(log, Does.Not.Contain("[Bot] stuck"));
        }

        [Test]
        public void WithoutTheLadderRuleABotDoesNotWalkOffTowardTheLadder()
        {
            Start(
                BotTestMaps.Ladder(BotTestMaps.Points(BotTestMaps.LowerSpawn, BotTestMaps.UpperSpawn)),
                Rules(ladder: false),
                bots: 2,
                spec: SightOf(1f));
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Assert.That(_context.Bodies.TryGet(botId, out var body), Is.True);
            Step(10f);

            Assert.That(body.Position.Y, Is.GreaterThan(BotTestMaps.PlatformY), _log.ToString());
            Assert.That(_log.ToString(), Does.Not.Contain("[Bot] ladder player="));
        }

        [Test]
        public void ABotGoesThroughABreakableWallOnlyAfterItBreaks()
        {
            // 書き出した NavMesh は壊せる建物をくり抜いていないので、床の NavMesh は壁の向こうまで続いている
            const int wallId = 50;
            var wall = WorldCollider.Box(
                wallId, 0, MapColliderGroups.Piece(0, 0), new Vector3(0f, 2f, 0f), new Vector3(30f, 2f, 0.25f), Quaternion.Identity);
            var structures = new NetMapStructures(new[] { new StructureLayout(_wallHealth, _wallGrounded, Array.Empty<(int A, int B)>()) });
            var points = BotTestMaps.Points(new Vector3(0f, 1.1f, -10f), new Vector3(0f, 1.1f, 10f));
            Start(BotTestMaps.Flat(points, structures, new[] { wall }), Rules(), bots: 2, spec: SightOf(1f));
            Join(1);
            Tick();
            _context.Bodies.Teleport(1, new Vector3(-25f, 1.1f, -25f), 0f, _host.TickCount);
            var botId = _bots.BotIds.Single();
            Assert.That(_context.Bodies.TryGet(botId, out var body), Is.True);
            var walkedTowardTheWall = 0f;
            for (var elapsed = 0f; elapsed < 10f; elapsed += _tickSeconds)
            {
                Tick();
                Assert.That(body.Position.Z, Is.GreaterThan(0.5f), _log.ToString());
                walkedTowardTheWall = Math.Max(walkedTowardTheWall, 10f - body.Position.Z);
            }

            Assert.That(walkedTowardTheWall, Is.LessThan(2f), "an intact wall must not be walked into");

            _combat.Structures.Enabled = true;
            Assert.That(_combat.Structures.ApplyHit(wallId, new Vector3(0f, 2f, 0f), 1000, 0f, null, _log), Is.EqualTo(1));
            var crossed = false;
            for (var elapsed = 0f; elapsed < 20f && !crossed; elapsed += _tickSeconds)
            {
                Tick();
                crossed = body.Position.Z < -5f;
            }

            Assert.That(crossed, Is.True, _log.ToString());
        }

        // 人 (Alpha) は道筋から離れた出現地点に立ったまま。Bot (Bravo) だけが攻め側なので、台車が進めば Bot が押している
        [Test]
        public void ABotGoesToThePayloadAndPushesIt()
        {
            Start(BotTestMaps.Flat(PayloadPoints()), Rules().WithObjective(MatchObjective.Payload), bots: 2, spec: SightOf(1f));
            Join(1);
            Tick();
            Assert.That(_bots.Count, Is.EqualTo(1));
            var botId = _bots.BotIds.Single();
            Assert.That(TeamOf(botId), Is.EqualTo(Team.Bravo));
            var rules = (PayloadRules)_rules.Objective;
            Assert.That(_log.ToString(), Does.Contain("[Bot] rally x=0.00 y=0.00 z=8.00 radius=3.00 bots=1"));

            Step(15f);

            Assert.That(rules.Payload.Distance, Is.GreaterThan(1d), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Payload] cart=pushing pushers=1 defenders=0"));
            Assert.That(_log.ToString(), Does.Contain("[Bot] rally x=0.00 y=0.00 z=6.").Or.Contain("[Bot] rally x=0.00 y=0.00 z=5."),
                "the bots are sent after the cart once it has moved half its radius");
        }

        [Test]
        public void BotsPatrolTheSpawnPointsWhenTheObjectiveNamesNoPlace()
        {
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 2);
            Join(1);
            Step(1f);

            Assert.That(_log.ToString(), Does.Not.Contain("[Bot] rally"));
        }

        // 人は離れた所に置き、Bot に見えないようにする。行き先を示している間は出現地点を巡らずそこへ向かう
        [Test]
        public void ABotGoesToTheGoalTheObjectiveGivesAndReturnsToPatrolWhenItIsTakenAway()
        {
            var goal = new Vector3(6f, 1.1f, -6f);
            var objective = new TestObjectiveRules();
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 2, spec: SightOf(1f), objective: objective);
            Join(1);
            Tick();
            _context.Bodies.Teleport(1, new Vector3(-25f, 1.1f, -25f), 0f, _host.TickCount);
            var botId = _bots.BotIds.Single();
            Assert.That(_context.Bodies.TryGet(botId, out var body), Is.True);
            objective.Goal = id => id == botId ? goal : null;

            Step(12f);

            Assert.That(Vector3.Distance(Flat(body.Position), Flat(goal)), Is.LessThan(1.5f), _log.ToString());
            Assert.That(_bots.TryGetAgent(botId, out var agent), Is.True);
            Assert.That(agent.Goal, Is.EqualTo(goal));
            var goalLine = "[Bot] goal player=" + Text(botId) + " x=6.00 y=1.10 z=-6.00";
            Assert.That(_log.ToString().Split('\n').Count(line => line.Contains(goalLine, StringComparison.Ordinal)), Is.EqualTo(1),
                "the route is set once while the goal stays the same");

            objective.Goal = null;
            Tick();

            Assert.That(agent.Goal, Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Bot] goal player=" + Text(botId) + " cleared"));
        }

        [Test]
        public void ABotWithAGoalIsLeftOutOfTheRally()
        {
            var objective = new TestObjectiveRules { Rally = new ObjectiveRally(new Vector3(0f, 0f, 8f), 3f) };
            Start(BotTestMaps.Flat(FlatPoints()), Rules(), bots: 3, spec: SightOf(1f), objective: objective);
            Join(1);
            Tick();
            var withGoal = _bots.BotIds.First();
            objective.Goal = id => id == withGoal ? new Vector3(-6f, 1.1f, 6f) : null;
            objective.Rally = new ObjectiveRally(new Vector3(0f, 0f, -8f), 3f);

            Tick();

            Assert.That(_bots.Count, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Bot] rally x=0.00 y=0.00 z=8.00 radius=3.00 bots=2"), "both bots rally before the goal");
            Assert.That(_log.ToString(), Does.Contain("[Bot] rally x=0.00 y=0.00 z=-8.00 radius=3.00 bots=1"), "the bot with a goal is not counted");
            Assert.That(_bots.TryGetAgent(withGoal, out var agent) && agent.Goal.HasValue, Is.True);
        }

        [Test]
        public void TheRallyRouteCrossesTheAreaAndTurnsForEachBot()
        {
            var center = new Vector3(0f, 0f, 8f);

            var first = ServerBots.RouteAround(center, 3f, 0);
            var second = ServerBots.RouteAround(center, 3f, 1);

            Assert.That(first, Has.Length.EqualTo(2));
            Assert.That(Vector3.Distance(first[0], new Vector3(1.5f, 0f, 8f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(first[1], new Vector3(-1.5f, 0f, 8f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(second[0], center), Is.EqualTo(1.5f).Within(1e-5f), "half the radius from the middle");
            Assert.That(second[0].Y, Is.Zero);
            Assert.That(Vector3.Distance(second[0], first[0]), Is.GreaterThan(1f), "the next bot crosses at another angle");
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerBots.RouteAround(center, 0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerBots.RouteAround(center, 3f, -1));
        }

        // 道筋は z = 8 から z = -8 へまっすぐ。押すのは Bravo (Bot)、出現地点はどちらも道筋から 10m 以上離す
        private static NetMapPoints PayloadPoints()
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-12f, 1.1f, -12f), Team.Alpha), new SpawnPoint(new Vector3(12f, 1.1f, 12f), Team.Bravo) },
                Array.Empty<ControlPointArea>(),
                null,
                new NetMapPayload(Team.Bravo, new[] { new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, -8f) }, null, 3f, 4f));
        }

        private static bool Near(Vector3 position, Vector3 spawn)
        {
            return Vector3.Distance(Flat(position), Flat(spawn)) < 1.5f && Math.Abs(position.Y - spawn.Y) < 0.5f;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        private sealed class BlockEverything : ISightBlocker
        {
            public bool Blocks(Vector3 from, Vector3 to) => true;
        }

        private static NetMapPoints FlatPoints()
        {
            return BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f));
        }

        private static NetBotSpec SightOf(float range)
        {
            var defaults = NetBotSpec.Default;
            return new NetBotSpec(
                new EnemySpec(range, Math.Min(range, defaults.Brain.AttackRange), defaults.Brain.LoseSightSeconds, defaults.Brain.SearchSeconds, defaults.Brain.ReactionSeconds),
                defaults.Input,
                defaults.NavSampleRadius,
                defaults.RepathSeconds,
                defaults.RepathDistance,
                defaults.StuckSeconds,
                defaults.StuckProgress);
        }

        private static GameRules Rules(bool ladder = true)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithLadder(ladder ? Ladder.On : Ladder.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(ServerMapData map, GameRules rules, int bots, NetBotSpec spec = null, IMatchObjectiveRules objective = null)
        {
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null, bots: spec);
            var settings = ServerModules.MotorSettingsFrom(data);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null, null, bots, 0, 0, 0, 0);
            _context = new ServerContext(options, "1.0.0", _log, map, _match, new ServerMessenger(_transport), _control, data);
            _movement = new ServerMovement(settings);
            _rules = objective == null ? new MatchRulesModule() : new MatchRulesModule((_, _, _) => objective);
            var combat = new ServerCombat(_rules, _movement);
            _combat = combat;
            _bots = new ServerBots(_rules, _movement, combat);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(combat);
            _host.Register(_bots);
            _host.Register(new ServerSnapshots(rules, new ISnapshotContributor[] { _movement, _rules, combat }));
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Leave(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Leave(connection);
            _host.OnDisconnected(connection);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        private static string Text(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
