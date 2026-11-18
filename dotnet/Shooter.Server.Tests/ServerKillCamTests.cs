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
    // 移動・ルール・戦闘・キルカムを同じホストで動かし、倒された人にだけ決めた秒数のクリップが届くことを確かめる
    public sealed class ServerKillCamTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const float _durationTolerance = 1e-3f;
        private const string _knifeId = "melee.knife";
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
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
        private ServerKillCam _killCam;

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
        public void OnlyTheKilledPlayerGetsTheKillersLastSeconds()
        {
            Start(KillCamRules());
            Join(1);
            Join(2);
            Join(3);
            Step(6f);
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(_rules.Session.GetTeam(1)));

            Assert.That(_combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null), Is.EqualTo(DamageOutcome.Killed));

            var clips = _transport.Read<KillCamClipMessage>(2, Copy);
            Assert.That(clips, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(clips[0].KillerId, Is.EqualTo(1));
            Assert.That(clips[0].VictimId, Is.EqualTo(2));
            var clip = clips[0].ToClip();
            Assert.That(clip.Duration, Is.EqualTo(KillCamBuffer.DefaultSeconds).Within(_durationTolerance));
            Assert.That(clip.Count, Is.InRange(240, 242), "one pose for each tick of 60 a second over 4 seconds");
            Assert.That(clip.PoseAt(clip.Count - 1), Is.EqualTo(new KillCamPose(Body(1).EyePosition, Body(1).Yaw)), "ends at the killer's eye");

            Assert.That(_transport.Read<KillCamClipMessage>(1, Copy), Is.Empty, "the killer gets nothing");
            Assert.That(_transport.Read<KillCamClipMessage>(3, Copy), Is.Empty, "a bystander gets nothing");
            Assert.That(_log.ToString(), Does.Contain("[KillCam] sent killer=1 victim=2 samples=" + clip.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " duration=4.00s"));
        }

        [Test]
        public void TheClipIsAsLongAsTheSecondsGivenToTheModule()
        {
            Start(KillCamRules(), 1.5f);
            Join(1);
            Join(2);
            Step(3f);

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null);

            var clip = _transport.Read<KillCamClipMessage>(1, Copy).Single();
            Assert.That(clip.KillerId, Is.EqualTo(2));
            Assert.That(clip.ToClip().Duration, Is.EqualTo(1.5f).Within(_durationTolerance));
            Assert.That(clip.ToClip().PoseAt(clip.Count - 1), Is.EqualTo(new KillCamPose(Body(2).EyePosition, Body(2).Yaw)));
            Assert.That(_transport.Read<KillCamClipMessage>(2, Copy), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[KillCam] server seconds=1.50 deathCamera=KillCam"));
        }

        [Test]
        public void AKillerWhoHasJustRespawnedSendsOnlyWhatHappenedSince()
        {
            Start(KillCamRules());
            Join(1);
            Join(2);
            Step(6f);
            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null);
            Assert.That(_rules.Session.IsAlive(1), Is.False);

            Step(NetMatchSettings.DefaultRespawnSeconds + 1f);
            Assert.That(_rules.Session.IsAlive(1), Is.True, _log.ToString());
            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null);

            var clip = _transport.Read<KillCamClipMessage>(2, Copy).Single().ToClip();
            Assert.That(clip.Duration, Is.GreaterThan(0.5f).And.LessThan(1.5f), "nothing from before the respawn about a second ago");
        }

        [Test]
        public void AClipLongerThanOneMessageHoldsIsThinnedButKeepsItsLength()
        {
            Start(KillCamRules(), 20f);
            Join(1);
            Join(2);
            Step(21f);

            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null);

            var clip = _transport.Read<KillCamClipMessage>(2, Copy).Single();
            Assert.That(clip.Count, Is.LessThanOrEqualTo(KillCamClipMessage.MaxSamples).And.GreaterThan(KillCamClipMessage.MaxSamples / 2));
            Assert.That(clip.ToClip().Duration, Is.EqualTo(20f).Within(_durationTolerance));
            Assert.That(clip.PoseAt(clip.Count - 1), Is.EqualTo(new KillCamPose(Body(1).EyePosition, Body(1).Yaw)));
        }

        [Test]
        public void NothingIsSentForAKillOfOnesOwn()
        {
            Start(KillCamRules());
            Join(1);
            Join(2);
            Step(5f);

            Assert.That(_combat.DealDamage(null, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_transport.Read<KillCamClipMessage>(2, Copy), Is.Empty);
            Assert.That(_transport.Read<KillCamClipMessage>(1, Copy), Is.Empty);
        }

        [Test]
        public void NothingIsRecordedOrSentWithoutTheKillCamRule()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(5f);

            Assert.That(_killCam.RecordingCount, Is.EqualTo(0));
            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, null);

            Assert.That(_transport.Read<KillCamClipMessage>(2, Copy), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[KillCam] server seconds=4.00 deathCamera=None"));
        }

        [Test]
        public void AKillerWithNoRecordSendsNothingAndSaysSo()
        {
            Start(KillCamRules());
            Join(1);
            Join(2);
            Step(5f);

            Assert.That(_killCam.SendClip(new KillFeedEntry(9, 2, _knifeId, false, false)), Is.False);

            Assert.That(_transport.Read<KillCamClipMessage>(2, Copy), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[KillCam] no record of killer=9 for victim=2; nothing sent"));
        }

        [Test]
        public void ALeavingPlayerTakesTheirRecordAlong()
        {
            Start(KillCamRules());
            Join(1);
            Join(2);
            Step(1f);
            Assert.That(_killCam.RecordingCount, Is.EqualTo(2));

            _match.Leave(new ConnectionId(1));
            _host.OnDisconnected(new ConnectionId(1));

            Assert.That(_killCam.RecordingCount, Is.EqualTo(1));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TheSecondsMustBePositiveAndFinite(float seconds)
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerKillCam(rules, combat, seconds));
        }

        [Test]
        public void FillThinsEvenlyAndKeepsTheLastPose()
        {
            var offsets = new List<float>();
            var poses = new List<KillCamPose>();
            for (var i = 0; i < 3000; i++)
            {
                offsets.Add(i * 0.01f);
                poses.Add(new KillCamPose(new Vector3(i, 0f, 0f), 0f));
            }

            var message = new KillCamClipMessage();
            ServerKillCam.Fill(message, 1, 2, new KillCamClip(offsets, poses));

            Assert.That(message.KillerId, Is.EqualTo(1));
            Assert.That(message.VictimId, Is.EqualTo(2));
            Assert.That(message.Count, Is.EqualTo(1001), "every third of 2999 gaps, then the last");
            Assert.That(message.PoseAt(1).Eye.X, Is.EqualTo(3f));
            Assert.That(message.PoseAt(message.Count - 1).Eye.X, Is.EqualTo(2999f));
            Assert.That(message.OffsetAt(message.Count - 1), Is.EqualTo(offsets[2999]));

            var shortClip = new KillCamClip(new List<float> { 0f, 0.5f, 1f }, new List<KillCamPose> { poses[0], poses[1], poses[2] });
            ServerKillCam.Fill(message, 3, 4, shortClip);
            Assert.That(message.Count, Is.EqualTo(3), "a clip that fits is sent whole and replaces the previous one");
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private static GameRules KillCamRules()
        {
            return Rules().WithDeathCamera(DeathCamera.KillCam);
        }

        private void Start(GameRules rules, float seconds = KillCamBuffer.DefaultSeconds)
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
            _killCam = new ServerKillCam(_rules, _combat, seconds);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_killCam);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
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

        private static KillCamClipMessage Copy(KillCamClipMessage m)
        {
            var copy = new KillCamClipMessage { KillerId = m.KillerId, VictimId = m.VictimId };
            for (var i = 0; i < m.Count; i++)
            {
                copy.Add(m.OffsetAt(i), m.PoseAt(i));
            }

            return copy;
        }
    }
}
