using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class MatchRulesModuleTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private MatchRulesModule _module;
        private StubControl _control;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
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
        public void TeamDeathmatchIsWonAtTheScoreLimitAndRestartsAfterTheDelay()
        {
            Start(Plain(), new NetMatchSettings(scoreLimit: 2, respawnSeconds: 3f, restartDelaySeconds: 5f));
            Join(1);
            Join(2);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Playing));

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));
            Advance(3.1f);
            _module.ApplyDamage(1, 2, 100);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_log.ToString(), Does.Contain("[Match] kill killer=1 victim=2"));
            Assert.That(_log.ToString(), Does.Contain("[Match] ended winner=Alpha"));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\"").And.Contain("\"scoreAlpha\":\"2\""));
            Assert.That(JsonOf("kill"), Has.Count.EqualTo(2));
            Assert.That(JsonOf("kill")[0], Does.Contain("\"killerId\":\"1\"").And.Contain("\"victimId\":\"2\"").And.Contain("\"teamKill\":\"false\""));
            var feed = _transport.Read<KilledMessage>(2, Copy);
            Assert.That(feed, Has.Count.EqualTo(2));
            Assert.That(feed[0].KillerId, Is.EqualTo(1));
            Assert.That(_transport.Read<KilledMessage>(1, Copy), Has.Count.EqualTo(2));
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            Assert.That(snapshot.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.NextMatchSeconds, Is.EqualTo(5));

            Advance(5.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.Session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(_match.AlphaScore, Is.Zero);
            var player = default(PlayerSnapshot);
            Assert.That(_module.FillPlayer(1, ref player), Is.True);
            Assert.That(player.Kills, Is.Zero);
        }

        [Test]
        public void TeamDeathmatchLosesOnKillsWhenTimeRunsOut()
        {
            Start(Plain(), new NetMatchSettings(timeLimitSeconds: 2f));
            Join(1);
            Join(2);
            _module.ApplyDamage(2, 1, 100);

            Advance(2.1f);

            Assert.That(_log.ToString(), Does.Contain("[Match] ended winner=Bravo"));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Bravo\""));
        }

        [Test]
        public void DominationCapturesNeutralizesAndWinsOnPoints()
        {
            var settings = new NetMatchSettings(captureSeconds: 1f, pointsPerSecond: 10f, dominationScoreLimit: 30, timeLimitSeconds: 100f);
            Start(Plain().WithObjective(MatchObjective.Domination), settings, controlPoints: 3);
            Assert.That(_log.ToString(), Does.Contain("[Domination] net points=3 scoreLimit=30 captureSeconds=1 pointsPerSecond=10 maxCapturers=3"));
            Join(1);
            Join(2);

            Place(1, new Vector3(0f, 1f, -15f));
            Advance(1.1f);
            Assert.That(_log.ToString(), Does.Contain("[Domination] captured point=A team=Alpha"));
            var points = new SnapshotList<ControlPointSnapshot>(SnapshotMessage.MaxControlPoints, "control point");
            _module.FillControlPoints(points);
            Assert.That(points.Count, Is.EqualTo(3));
            Assert.That(points[0].HasOwner, Is.True);
            Assert.That(points[0].Owner, Is.EqualTo(Team.Alpha));
            Assert.That(points[0].ControlPercent, Is.EqualTo(100));
            Assert.That(points[1].HasOwner, Is.False);

            Place(1, new Vector3(30f, 1f, 30f));
            Place(2, new Vector3(0f, 1f, -15f));
            Advance(1.1f);
            Assert.That(_log.ToString(), Does.Contain("[Domination] neutralized point=A from=Alpha"));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));

            Advance(5f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Bravo\""));
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(snapshot.BravoPoints, Is.GreaterThanOrEqualTo(30));
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void DominationIsNotEndedByKills()
        {
            Start(Plain().WithObjective(MatchObjective.Domination), new NetMatchSettings(scoreLimit: 1, respawnSeconds: 0f), controlPoints: 3);
            Join(1);
            Join(2);

            _module.ApplyDamage(1, 2, 100);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));
        }

        [Test]
        public void DominationWithoutControlPointsDoesNotStart()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Plain().WithObjective(MatchObjective.Domination)));

            Assert.That(e.Message, Does.Contain("has no control points"));
        }

        [Test]
        public void DownedAllyIsRevivedWhileHeldInRange()
        {
            Start(Plain(downed: true));
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Place(1, new Vector3(0f, 1f, 0f));
            Place(3, new Vector3(1f, 1f, 0f));

            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(_log.ToString(), Does.Contain("[Net] downed attacker=2 victim=1"));
            var player = default(PlayerSnapshot);
            _module.FillPlayer(1, ref player);
            Assert.That(player.Downed.IsDowned, Is.True);
            Assert.That(player.Downed.BleedoutSeconds, Is.EqualTo(5));
            Assert.That(_module.CanFire(1), Is.False);

            for (var elapsed = 0f; elapsed < 1.2f; elapsed += _tickSeconds)
            {
                _module.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
                Step();
            }

            Assert.That(_log.ToString(), Does.Contain("[Net] revived reviver=3 target=1"));
            _module.FillPlayer(1, ref player);
            Assert.That(player.Downed.IsDowned, Is.False);
            Assert.That(player.Health, Is.EqualTo(30));
            Assert.That(_module.CanFire(1), Is.True);
        }

        [Test]
        public void ReviveFromTooFarIsRejectedAndTheDownedBleedOut()
        {
            Start(Plain(downed: true));
            Join(1);
            Join(2);
            Join(3);
            Place(1, new Vector3(0f, 1f, 0f));
            Place(3, new Vector3(9f, 1f, 0f));
            _module.ApplyDamage(2, 1, 100);

            _module.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
            Assert.That(_module.ReviveHoldCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected revive reviver=3 target=1 reason=TooFar"));

            Advance(5.1f);

            Assert.That(_log.ToString(), Does.Contain("[Net] bleedout player=1 killer=2"));
            Assert.That(_module.Session.IsAlive(1), Is.False);
            Assert.That(_match.BravoScore, Is.EqualTo(1));
        }

        [Test]
        public void PingIsSharedWithTheTeamAndWrittenAsJson()
        {
            Start(Plain(ping: true));
            Join(1);
            Join(2);
            Place(1, new Vector3(0f, 1.5f, 0f));
            Place(2, new Vector3(0f, 1.5f, 30f));

            Assert.That(_module.ServerPing(1, new Vector3(0f, -1f, 1f)), Is.EqualTo(PingRefusal.None));

            var placed = _transport.Read<PingPlacedMessage>(1, Copy);
            Assert.That(placed, Has.Count.EqualTo(1));
            Assert.That(placed[0].Kind, Is.EqualTo(PingKind.Location));
            Assert.That(placed[0].Position.Y, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(_transport.Read<PingPlacedMessage>(2, Copy), Is.Empty);
            Assert.That(JsonOf("ping_placed").Single(),
                Does.Contain("\"playerId\":\"1\"").And.Contain("\"team\":\"Alpha\"").And.Contain("\"kind\":\"Location\"").And.Contain("\"refreshed\":\"false\""));

            Assert.That(_module.ServerPing(1, new Vector3(0f, -1f, 1f)), Is.EqualTo(PingRefusal.TooSoon));
            Assert.That(JsonOf("ping_refused").Single(), Does.Contain("\"playerId\":\"1\"").And.Contain("\"reason\":\"TooSoon\""));

            Advance(1f);
            Assert.That(_module.ServerPing(1, new Vector3(0f, 0f, 1f)), Is.EqualTo(PingRefusal.None));
            Assert.That(JsonOf("ping_placed")[1], Does.Contain("\"kind\":\"Enemy\"").And.Contain("\"targetId\":\"2\""));
        }

        [Test]
        public void PingIsRefusedWhenTheRuleIsOff()
        {
            Start(Plain());
            Join(1);

            Assert.That(_module.ServerPing(1, Vector3.UnitZ), Is.EqualTo(PingRefusal.RuleOff));
            Assert.That(_log.ToString(), Does.Contain("[Ping] net refused player=1 reason=RuleOff"));
        }

        [Test]
        public void JoiningSpawnsOnTheTeamsPointWithProtectionUntilFiring()
        {
            Start(Plain().WithSpawnProtectionSeconds(2f));
            Join(1);

            var teleports = _transport.Read<TeleportedMessage>(1, Copy);
            Assert.That(teleports, Has.Count.EqualTo(1));
            Assert.That(teleports[0].PlayerId, Is.EqualTo(1));
            Assert.That(teleports[0].Position, Is.EqualTo(new Vector3(-20f, 1f, -20f)));
            Assert.That(_context.Bodies.TryGet(1, out var body), Is.True);
            Assert.That(body.Position, Is.EqualTo(new Vector3(-20f, 1f, -20f)));
            Assert.That(_log.ToString(), Does.Contain("[Match] spawn protection player=1 seconds=2"));
            var player = default(PlayerSnapshot);
            _module.FillPlayer(1, ref player);
            Assert.That(player.SpawnProtectionSeconds, Is.EqualTo(2));
            Assert.That(player.Team, Is.EqualTo(Team.Alpha));
            Assert.That(player.Health, Is.EqualTo(NetMatchSettings.DefaultMaxHealth));

            _module.NotifyFired(1);

            _module.FillPlayer(1, ref player);
            Assert.That(player.SpawnProtectionSeconds, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Match] spawn protection ended player=1 reason=fired"));
        }

        [Test]
        public void ChosenLoadoutIsAnsweredAndGivenFromTheNextSpawn()
        {
            Start(Plain());
            Join(1);
            var spawned = new List<int>();
            _module.PlayerSpawned += spawned.Add;

            var request = new LoadoutRequestMessage();
            request.WeaponIds.Add(TestRules.HandgunId);
            request.WeaponIds.Add(TestRules.RifleId);
            _module.OnMessage(new ConnectionId(1), request);
            var bad = new LoadoutRequestMessage();
            bad.WeaponIds.Add("not-a-weapon");
            _module.OnMessage(new ConnectionId(1), bad);

            var answers = _transport.Read<LoadoutAnswerMessage>(1, Copy);
            Assert.That(answers, Has.Count.EqualTo(2));
            Assert.That(answers[0].Accepted, Is.True);
            Assert.That(answers[1].Accepted, Is.False);
            Assert.That(answers[1].Reason, Is.Not.Empty);
            Assert.That(_module.WeaponsFor(1)[0].Id, Is.EqualTo(TestRules.HandgunId));
            Assert.That(_log.ToString(), Does.Contain("[Net] loadout accepted client=1"));
            Assert.That(_log.ToString(), Does.Contain("[Net] loadout rejected client=1"));

            Join(2);
            _module.ApplyDamage(2, 1, 100);
            Advance(NetMatchSettings.DefaultRespawnSeconds + 0.1f);

            Assert.That(spawned, Does.Contain(1));
        }

        [Test]
        public void IdlePlayersAreWarnedThenKicked()
        {
            Start(Plain().WithIdleKickSeconds(20f));
            Join(1);
            Join(2);

            Advance(15.5f);
            Assert.That(_log.ToString(), Does.Contain("[Match] idle warning player=1"));
            var warnings = _transport.Read<IdleWarningMessage>(1, Copy);
            Assert.That(warnings.Last().Active, Is.True);

            var input = new InputMessage();
            input.Add(new InputFrame(1, new Vector2(0f, 1f), 0f, InputButtons.None));
            _module.OnMessage(new ConnectionId(1), input);
            Assert.That(_transport.Read<IdleWarningMessage>(1, Copy).Last().Active, Is.False);
            Assert.That(_control.Kicks, Is.Empty);

            Advance(5f);
            Assert.That(_control.Kicks.Select(k => k.Connection.Value), Is.EqualTo(new ulong[] { 2 }));
            Assert.That(_control.Kicks[0].Reason, Is.EqualTo(DisconnectNotice.IdleKickedReason));

            Advance(15.5f);

            Assert.That(_control.Kicks.Select(k => k.Connection.Value), Is.EqualTo(new ulong[] { 2, 1 }));
            Assert.That(_log.ToString(), Does.Contain("[Match] idle kick player=1 client=1 after=20s"));
        }

        [Test]
        public void SameInputIsNotActivity()
        {
            var still = new InputFrame(1, Vector2.Zero, 0.5f, InputButtons.Crouch);

            Assert.That(MatchRulesModule.IsActivity(still, new InputFrame(2, Vector2.Zero, 0.5f, InputButtons.Crouch)), Is.False);
            Assert.That(MatchRulesModule.IsActivity(still, new InputFrame(2, Vector2.Zero, 0.6f, InputButtons.Crouch)), Is.True);
            Assert.That(MatchRulesModule.IsActivity(still, new InputFrame(2, Vector2.Zero, 0.5f, InputButtons.Crouch | InputButtons.Jump)), Is.True);
            Assert.That(MatchRulesModule.IsActivity(still, new InputFrame(2, Vector2.Zero, 0.5f, InputButtons.None)), Is.True);
        }

        [Test]
        public void ReconnectingPlayerReturnsToItsTeamWithItsScore()
        {
            Start(Plain());
            Join(1, "key1");
            Join(2, "key2");
            _module.ApplyDamage(1, 2, 100);

            _match.Leave(new ConnectionId(1));
            Assert.That(_log.ToString(), Does.Contain("[Match] holding player=1 for=60s"));
            Join(3, "key3");
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Join(4, "key1");

            Assert.That(TeamOf(4), Is.EqualTo(Team.Alpha));
            Assert.That(_module.Session.GetKills(4), Is.EqualTo(1));
            Assert.That(JsonOf("player_joined").Last(), Does.Contain("\"playerId\":\"4\"").And.Contain("\"rejoined\":\"true\""));
            Assert.That(_log.ToString(), Does.Contain("[Match] rejoined player=4"));
        }

        [Test]
        public void ReflectedAndTeamKillsAreTold()
        {
            Start(Plain().WithFriendlyFire(FriendlyFire.On));
            Join(1);
            Join(2);
            Join(3);

            _module.ApplyDamage(3, 1, 100, new KillShot(3, 1, TestRules.RifleId, HitZone.Head));

            Assert.That(_log.ToString(), Does.Contain("[Match] teamkill killer=3 victim=1"));
            Assert.That(JsonOf("kill").Single(), Does.Contain("\"teamKill\":\"true\""));
            var feed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(feed.Friendly, Is.True);
            Assert.That(feed.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(feed.Headshot, Is.True);
        }

        [Test]
        public void MapWithoutPointsCannotStartTheRules()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Plain(), points: false));

            Assert.That(e.Message, Does.Contain("NetTest.points.json"));
        }

        [Test]
        public void BodiesUseTheBodySizeFromTheRules()
        {
            var motor = new NetMotorSettings(MotorSpec.Default, new StanceGeometry(1.8f, 0.9f, 1.6f, 1.1f, 0.4f), CapsuleMoverSpec.Default);
            var data = new ServerRulesData("TeamDeathmatch", new NetMatchFile(
                "TeamDeathmatch", Plain(), NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { TestRules.Rifle(), TestRules.Handgun() }, 0.4f, null, null, motor), PingSpec.Default, "memory", null);

            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0", _log, TestMaps.Data(), new ServerMatch(0f, _log, new ServerEventWriter(false, _json, _log), () => _time),
                new ServerMessenger(_transport), new StubControl(), data);

            Assert.That(context.Bodies.Geometry, Is.SameAs(motor.Stance));
            Assert.That(data.Motor, Is.SameAs(motor));
            var body = context.Bodies.Add(1);
            body.SetPose(Vector3.Zero, 0f, PlayerStance.Standing);
            Assert.That(body.EyePosition.Y, Is.EqualTo(1.6f));
            Assert.That(body.Radius, Is.EqualTo(0.4f));
        }

        [Test]
        public void RayCapsuleFindsTheNearSide()
        {
            Assert.That(
                MatchRulesModule.RayCapsule(Vector3.Zero, Vector3.UnitZ, new Vector3(0f, -1f, 5f), new Vector3(0f, 1f, 5f), 0.5f, out var distance),
                Is.True);
            Assert.That(distance, Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(
                MatchRulesModule.RayCapsule(Vector3.Zero, Vector3.UnitZ, new Vector3(2f, -1f, 5f), new Vector3(2f, 1f, 5f), 0.5f, out _),
                Is.False);
            Assert.That(
                MatchRulesModule.RayCapsule(Vector3.Zero, Vector3.UnitZ, new Vector3(0f, -1f, -5f), new Vector3(0f, 1f, -5f), 0.5f, out _),
                Is.False);
        }

        private static GameRules Plain(bool downed = false, bool ping = false)
        {
            var actions = ActionRules.Default
                .WithDowned(downed ? Downed.Revive : Downed.Off)
                .WithDownedBleedoutSeconds(5f)
                .WithReviveSeconds(1f)
                .WithPing(ping ? Ping.On : Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(GameRules rules, NetMatchSettings settings = null, int controlPoints = 0, bool points = true)
        {
            settings ??= NetMatchSettings.Default;
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", points ? TestRules.Points(controlPoints) : null),
                _match,
                new ServerMessenger(_transport),
                _control,
                TestRules.Data(rules, settings));
            var module = new MatchRulesModule();
            module.Start(_context);
            _module = module;
        }

        private void Join(int id, string key = null)
        {
            _match.Join(new ConnectionId((ulong)id), key ?? "device" + id);
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

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }

        // 蹴られた人は ServerMatch から抜けるはずなので、MatchServer がするように抜く
        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Step();
                foreach (var kick in _control.Kicks)
                {
                    _match.Leave(kick.Connection);
                }
            }
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }

        private static T Copy<T>(T message)
            where T : class, IMessage
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            var table = MatchMessages.CreateClientTable();
            var copy = (T)table.Read(writer.AsSegment());
            return copy;
        }
    }
}
