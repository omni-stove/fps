using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesReviveRangeTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

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
        public void AnAllyTwoAndAHalfMetersAwayIsRevivedWhenTheRulesReachThree()
        {
            Start(3f);
            JoinAndDown(new Vector3(2.5f, 1f, 0f));

            HoldRevive(1.2f);

            Assert.That(_log.ToString(), Does.Contain("[Net] revived reviver=3 target=1"));
            Assert.That(_log.ToString(), Does.Not.Contain("reason=TooFar"));
            Assert.That(_module.Session.IsDowned(1), Is.False);
        }

        [Test]
        public void TheSameDistanceIsTooFarForTheDefaultReach()
        {
            Start(ReviveReach.DefaultRange);
            JoinAndDown(new Vector3(2.5f, 1f, 0f));

            _module.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });

            Assert.That(_module.ReviveHoldCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected revive reviver=3 target=1 reason=TooFar"));
            Assert.That(_module.Session.IsDowned(1), Is.True);
        }

        [Test]
        public void AHoldIsDroppedOnceTheReviverWalksOutOfTheRulesReach()
        {
            Start(3f);
            JoinAndDown(new Vector3(2.5f, 1f, 0f));
            _module.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
            Assert.That(_module.ReviveHoldCount, Is.EqualTo(1));

            Place(3, new Vector3(3.5f, 1f, 0f));
            _module.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
            Step();

            Assert.That(_module.ReviveHoldCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected revive reviver=3 target=1 reason=TooFar"));
        }

        [Test]
        public void TheStartLogShowsTheRulesReach()
        {
            Start(3f);

            Assert.That(_log.ToString(), Does.Contain("[Net] downed rules downed=Revive ").And.Contain(" range=3.00m"));
        }

        private void JoinAndDown(Vector3 reviverPosition)
        {
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(3), Is.EqualTo(TeamOf(1)));
            Place(1, new Vector3(0f, 1f, 0f));
            Place(3, reviverPosition);
            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Downed));
        }

        private void HoldRevive(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _module.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
                Step();
            }
        }

        private void Start(float reviveRange)
        {
            var actions = ActionRules.Default
                .WithDowned(Downed.Revive)
                .WithDownedBleedoutSeconds(5f)
                .WithReviveSeconds(1f)
                .WithReviveRange(reviveRange);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var settings = NetMatchSettings.Default;
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points(0)),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(rules, settings));
            var module = new MatchRulesModule();
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

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }
    }
}
