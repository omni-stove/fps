using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerViolationsTests
    {
        private const float _window = 10f;
        private const int _shotLimit = 3;
        private const int _budgetLimit = 4;
        private const string _rejected = "shooter:rejected:anti-cheat";
        private static readonly ConnectionId _alice = new ConnectionId(3);
        private static readonly ConnectionId _bob = new ConnectionId(4);

        private StringWriter _log;
        private RecordingTransport _transport;
        private StubControl _control;
        private ServerMatch _match;
        private ServerCheatSignals _signals;
        private ServerViolations _module;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
            _signals = new ServerCheatSignals();
            _match = new ServerMatch(0f, _log, new ServerEventWriter(false, _log, _log), () => DateTime.UtcNow);
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void ShotRejectionsExactlyAtTheLimitDoNotKick()
        {
            Start();
            Join(_alice);

            Reject(3, _shotLimit);

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void OneShotRejectionOverTheLimitKicksWithTheAntiCheatReasonAndLogsWhy()
        {
            Start();
            Join(_alice);

            Reject(3, _shotLimit + 1);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, _rejected) }));
            Assert.That(_log.ToString(), Does.Contain(
                "[AntiCheat] violations kicking id=3: reason=ShotRejected count=4 limit=3 window=10s"));
        }

        [Test]
        public void InputsOverBudgetExactlyAtTheLimitDoNotKick()
        {
            Start();
            Join(_alice);

            OverBudget(3, _budgetLimit);

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void OneInputOverBudgetPastTheLimitKicks()
        {
            Start();
            Join(_alice);

            OverBudget(3, _budgetLimit + 1);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, _rejected) }));
            Assert.That(_log.ToString(), Does.Contain(
                "[AntiCheat] violations kicking id=3: reason=InputOverBudget count=5 limit=4 window=10s"));
        }

        [Test]
        public void ShotRejectionsAndInputsOverBudgetAreCountedAgainstTheirOwnLimits()
        {
            Start();
            Join(_alice);

            Reject(3, _shotLimit);
            OverBudget(3, _shotLimit + 1);

            Assert.That(_control.Kicks, Is.Empty, "inputs over budget are held to their own, larger limit");
        }

        [Test]
        public void RejectionsOlderThanTheWindowAreNotCounted()
        {
            Start();
            Join(_alice);

            Reject(3, _shotLimit);
            Advance(_window + 0.5f);
            Reject(3, _shotLimit);

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void RejectionsStillInsideTheWindowAreCounted()
        {
            Start();
            Join(_alice);

            Reject(3, _shotLimit);
            Advance(_window - 0.5f);
            Reject(3, 1);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, _rejected) }));
        }

        [Test]
        public void OnlyTheRejectionsThatLeftTheWindowAreDropped()
        {
            Start();
            Join(_alice);

            Reject(3, 2);
            Advance(_window / 2f);
            Reject(3, 1);
            Advance(_window / 2f + 0.5f);
            Reject(3, 2);

            Assert.That(_control.Kicks, Is.Empty, "the first two have left the window, the third is still in it");

            Reject(3, 1);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, _rejected) }));
        }

        [Test]
        public void WhenDisabledNothingIsCountedOrKicked()
        {
            Start(enabled: false);
            Join(_alice);

            Reject(3, _shotLimit * 10);
            OverBudget(3, _budgetLimit * 10);

            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain(
                "[AntiCheat] violations off (anticheat.json violations.enabled is false)"));
            Assert.That(_log.ToString(), Does.Not.Contain("violations kicking"));
        }

        [Test]
        public void WhenEnabledTheStartLogsTheWindowAndLimits()
        {
            Start();

            Assert.That(_log.ToString(), Does.Contain(
                "[AntiCheat] violations on window=10s shotRejectionLimit=3 overBudgetLimit=4"));
        }

        [Test]
        public void BotsAreNotCounted()
        {
            Start();
            var bot = BotRoster.FirstBotPlayerId;
            _match.JoinBot(bot);

            Reject(bot, _shotLimit * 10);
            OverBudget(bot, _budgetLimit * 10);

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void RejectionsWhileDodgingOrVaultingAreNotCounted()
        {
            Start();
            Join(_alice);

            for (var i = 0; i < _shotLimit * 10; i++)
            {
                _signals.RaiseShotRejected(3, ShotRejection.Dodging);
                _signals.RaiseShotRejected(3, ShotRejection.Vaulting);
            }

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void OnlyRejectionsWhileDodgingOrVaultingAreLeftOut()
        {
            foreach (ShotRejection reason in Enum.GetValues(typeof(ShotRejection)))
            {
                var counted = reason != ShotRejection.Dodging && reason != ShotRejection.Vaulting;
                Assert.That(ServerViolations.IsCounted(reason), Is.EqualTo(counted), reason.ToString());
            }
        }

        [Test]
        public void EachPlayerIsCountedSeparately()
        {
            Start();
            Join(_alice);
            Join(_bob);

            Reject(3, _shotLimit);
            Reject(4, _shotLimit);

            Assert.That(_control.Kicks, Is.Empty);

            Reject(4, 1);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_bob, _rejected) }));
        }

        [Test]
        public void AKickedPlayerIsKickedOnceEvenIfMoreArriveBeforeTheyLeave()
        {
            Start();
            Join(_alice);

            Reject(3, _shotLimit * 3);
            OverBudget(3, _budgetLimit * 3);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, _rejected) }));
        }

        [Test]
        public void ThePlayerWhoLeftIsForgottenAndStartsFromZeroOnReturning()
        {
            Start();
            Join(_alice);
            Reject(3, _shotLimit);

            Leave(_alice);
            Join(_alice);
            Reject(3, _shotLimit);

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void AKickedPlayerWhoReturnsIsCountedAgain()
        {
            Start();
            Join(_alice);
            Reject(3, _shotLimit + 1);

            Leave(_alice);
            Join(_alice);
            Reject(3, _shotLimit + 1);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, _rejected), (_alice, _rejected) }));
        }

        [Test]
        public void SignalsForSomeoneNotInTheMatchAreIgnored()
        {
            Start();

            Reject(3, _shotLimit * 3);

            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void AfterStopNothingIsCounted()
        {
            Start();
            Join(_alice);
            _module.Stop();

            Reject(3, _shotLimit * 3);

            Assert.That(_control.Kicks, Is.Empty);
        }

        private void Start(bool enabled = true)
        {
            var settings = new AntiCheatTuning.ViolationSettings(enabled, _window, _shotLimit, _budgetLimit);
            _module = new ServerViolations(_signals, settings);
            _module.Start(new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "0.3.0",
                _log,
                TestMaps.Data(),
                _match,
                new ServerMessenger(_transport),
                _control));
        }

        private void Join(ConnectionId connection)
        {
            _match.Join(connection, "device" + connection.Value);
            _module.OnConnected(connection);
        }

        private void Leave(ConnectionId connection)
        {
            _match.Leave(connection);
            _module.OnDisconnected(connection);
        }

        private void Reject(int playerId, int times)
        {
            for (var i = 0; i < times; i++)
            {
                _signals.RaiseShotRejected(playerId, ShotRejection.FireRateExceeded);
            }
        }

        private void OverBudget(int playerId, int times)
        {
            for (var i = 0; i < times; i++)
            {
                _signals.RaiseInputOverBudget(playerId);
            }
        }

        private void Advance(float seconds)
        {
            // 1 tick の秒数を足していくのは、サーバーの tick と同じ数え方で窓を過ぎるかを確かめるため
            const float tick = 0.05f;
            var steps = (int)Math.Round(seconds / tick);
            for (var i = 0; i < steps; i++)
            {
                _module.Tick(i, tick);
            }
        }
    }
}
