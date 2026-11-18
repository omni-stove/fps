using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2StreakPartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;

        private readonly List<string> _logs = new List<string>();
        private readonly List<string> _sentRewards = new List<string>();
        private MatchClient _client;
        private NetV2StreakPart _part;
        private Streaks _rule;
        private bool _pressed;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Streak.Clear();
            _logs.Clear();
            _sentRewards.Clear();
            _rule = Streaks.On;
            _pressed = false;
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2StreakPart(_client, Send, () => _rule, _logs.Add, Spec(), () => null, () => _pressed);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            _client.Dispose();
            GameSignals.Streak.Clear();
        }

        [Test]
        public void TheOwnRowBecomesTheStreakSignal()
        {
            _part.Apply(Snapshot(kills: 1));

            var status = GameSignals.Streak.Value;
            Assert.That(status.HasValue, Is.True);
            Assert.That(status.Value.Kills, Is.EqualTo(1));
            Assert.That(status.Value.ReadyCount, Is.EqualTo(1));
            Assert.That(status.Value.Slots[0], Is.EqualTo(new StreakSlot(StreakSpec.UavRewardId, StreakSpec.DefaultUavNameKey, 1, true)));
        }

        [Test]
        public void WithStreaksOffNothingIsShownOrSent()
        {
            _rule = Streaks.Off;
            _part.Apply(Snapshot(kills: 3));
            _pressed = true;

            _part.Tick(0.1f);

            Assert.That(GameSignals.Streak.HasValue, Is.False);
            Assert.That(_sentRewards, Is.Empty);
        }

        [Test]
        public void PressingUseSendsTheNextReward()
        {
            _part.Apply(Snapshot(kills: 1));
            _pressed = true;

            _part.Tick(0.1f);

            Assert.That(_sentRewards, Is.EqualTo(new[] { StreakSpec.UavRewardId }));
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1), "the slot waits for the server");
        }

        [Test]
        public void NotPressingSendsNothing()
        {
            _part.Apply(Snapshot(kills: 1));

            _part.Tick(0.1f);

            Assert.That(_sentRewards, Is.Empty);
        }

        [Test]
        public void TheActivationIsTakenAndEmptiesTheShownSlot()
        {
            _part.Apply(Snapshot(kills: 1));

            var taken = _part.Handle(new StreakActivatedMessage { PlayerId = _localId, Team = Team.Alpha, RewardId = StreakSpec.UavRewardId, DurationSeconds = 20f });

            Assert.That(taken, Is.True);
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(0));
            Assert.That(_part.Counter.AlliedUavSecondsLeft, Is.EqualTo(20f));
        }

        [Test]
        public void OtherMessagesAreLeftToTheRest()
        {
            Assert.That(_part.Handle(new PingPlacedMessage()), Is.False);
        }

        [Test]
        public void DisposingClearsOnlyWhatThePartPublished()
        {
            _part.Apply(Snapshot(kills: 1));
            _part.Dispose();
            Assert.That(GameSignals.Streak.HasValue, Is.False);

            var other = new StreakStatus(2, Array.Empty<StreakSlot>());
            var otherPart = new NetV2StreakPart(_client, Send, () => Streaks.On, _logs.Add, Spec(), () => null, () => false);
            otherPart.Apply(Snapshot(kills: 0));
            GameSignals.Streak.Publish(other);
            otherPart.Dispose();

            Assert.That(GameSignals.Streak.Value, Is.EqualTo((StreakStatus?)other));
        }

        [Test]
        public void RefusesMissingInputs()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2StreakPart(null, Send, () => Streaks.On));
            Assert.Throws<ArgumentNullException>(() => new NetV2StreakPart(_client, null, () => Streaks.On));
            Assert.Throws<ArgumentNullException>(() => new NetV2StreakPart(_client, Send, null));
        }

        private static StreakSpec Spec()
        {
            return new StreakSpec(new[]
            {
                new StreakReward(StreakSpec.UavRewardId, StreakSpec.DefaultUavNameKey, 1),
                new StreakReward(StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, 3),
            });
        }

        private static SnapshotMessage Snapshot(int kills)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.StreakKills = kills;
            return snapshot;
        }

        private bool Send(IMessage message)
        {
            _sentRewards.Add(((StreakUseRequestMessage)message).RewardId);
            return true;
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
