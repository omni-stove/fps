using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientStreaksTests
    {
        private const int _localId = 3;
        private const int _otherId = 4;

        private readonly List<StreakUseRequestMessage> _sent = new List<StreakUseRequestMessage>();
        private readonly List<string> _logs = new List<string>();
        private bool _connected;
        private ClientStreaks _streaks;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _connected = true;
            _streaks = new ClientStreaks(_localId, Spec(), Send, _logs.Add);
        }

        [Test]
        public void NothingIsShownUntilTheOwnRowArrives()
        {
            Assert.That(_streaks.Status, Is.Null);

            Assert.That(_streaks.Apply(Snapshot(GameModeState.Playing, kills: 0, includeLocal: false)), Is.False);
            Assert.That(_streaks.Status, Is.Null);

            Assert.That(_streaks.Apply(Snapshot(GameModeState.Playing, kills: 0)), Is.True);
            Assert.That(_streaks.Status.Value.Kills, Is.EqualTo(0));
            Assert.That(_streaks.Status.Value.Slots, Has.Count.EqualTo(2));
            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void TheKillsFromTheSnapshotMakeTheSameSlotsAsTheServer()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 1));
            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(0));

            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));

            var status = _streaks.Status.Value;
            Assert.That(status.Kills, Is.EqualTo(2));
            Assert.That(status.Slots[0], Is.EqualTo(new StreakSlot(StreakSpec.UavRewardId, StreakSpec.DefaultUavNameKey, 2, true)));
            Assert.That(status.Slots[1], Is.EqualTo(new StreakSlot(StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, 4, false)));
            Assert.That(_logs, Has.Member(StreakLog.Earned("net3", StreakSpec.UavRewardId, status)));
        }

        [Test]
        public void AFallKeepsTheEarnedRewardAndCountsKillsAfterIt()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));

            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 1));

            var status = _streaks.Status.Value;
            Assert.That(status.Kills, Is.EqualTo(1), "fell and killed one more between the snapshots");
            Assert.That(status.Slots[0].Ready, Is.True);
        }

        [Test]
        public void UsingSendsTheNextRewardAndTheDirectionButKeepsTheSlotUntilTheServerAnswers()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));
            var direction = new Vector3(0f, -0.5f, 1f);

            Assert.That(_streaks.TryUse(direction, out var sent), Is.True);

            Assert.That(sent, Is.True);
            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].RewardId, Is.EqualTo(StreakSpec.UavRewardId));
            Assert.That(_sent[0].Direction, Is.EqualTo(direction));
            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(1));
            Assert.That(_logs, Has.Member(ClientStreakLog.Requested("net3", StreakSpec.UavRewardId, true)));
        }

        [Test]
        public void TheServersActivationEmptiesTheSlot()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));

            Assert.That(_streaks.Handle(Activated(_localId, Team.Alpha, StreakSpec.UavRewardId, 20f)), Is.True);

            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(0));
            Assert.That(_streaks.AlliedUavSecondsLeft, Is.EqualTo(20f));
            Assert.That(_streaks.EnemyUavSecondsLeft, Is.EqualTo(0f));
        }

        [Test]
        public void SomeoneElsesActivationOnlyTellsWhoseUavIsUp()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));

            Assert.That(_streaks.Handle(Activated(_otherId, Team.Bravo, StreakSpec.UavRewardId, 15f)), Is.False);
            _streaks.Tick(5f);

            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(1));
            Assert.That(_streaks.EnemyUavSecondsLeft, Is.EqualTo(10f));
            Assert.That(_streaks.AlliedUavSecondsLeft, Is.EqualTo(0f));

            _streaks.Tick(20f);
            Assert.That(_streaks.EnemyUavSecondsLeft, Is.EqualTo(0f));
        }

        [Test]
        public void AnActivationOfARewardNotReadyHereIsLoggedAndLeavesTheSlots()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));

            Assert.That(_streaks.Handle(Activated(_localId, Team.Alpha, StreakSpec.AirstrikeRewardId, 1f)), Is.False);

            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(1));
            Assert.That(_logs, Has.Member(ClientStreakLog.NotReadyHere("net3", StreakSpec.AirstrikeRewardId)));
        }

        [Test]
        public void NothingIsSentWithoutAReadyReward()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 1));

            Assert.That(_streaks.TryUse(Vector3.UnitZ, out var sent), Is.False);

            Assert.That(sent, Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(StreakLog.NothingToUse("net3")));
        }

        [Test]
        public void ARequestThatCouldNotBeSentIsReported()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));
            _connected = false;

            Assert.That(_streaks.TryUse(Vector3.UnitZ, out var sent), Is.True);

            Assert.That(sent, Is.False);
            Assert.That(_logs, Has.Member(ClientStreakLog.Requested("net3", StreakSpec.UavRewardId, false)));
        }

        [Test]
        public void ANonFiniteDirectionIsNotSent()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));

            Assert.That(_streaks.TryUse(new Vector3(float.NaN, 0f, 1f), out var sent), Is.False);

            Assert.That(sent, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ANewMatchClearsTheRewardsLikeTheServer()
        {
            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 2));
            _streaks.Apply(Snapshot(GameModeState.Ended, kills: 0));
            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(1), "a fall keeps the reward");

            _streaks.Apply(Snapshot(GameModeState.Playing, kills: 0));

            Assert.That(_streaks.Status.Value.ReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void TheRequestReadsBackOnTheServer()
        {
            StreakUseRequestMessage request = null;
            var streaks = new ClientStreaks(_localId, Spec(), message =>
            {
                var writer = new MessageWriter();
                message.Write(writer);
                request = new StreakUseRequestMessage();
                request.Read(new MessageReader(writer.ToArray()));
                return true;
            }, _logs.Add);
            streaks.Apply(Snapshot(GameModeState.Playing, kills: 4));

            streaks.TryUse(Vector3.UnitY, out _);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.RewardId, Is.EqualTo(StreakSpec.UavRewardId));
            Assert.That(request.Direction, Is.EqualTo(Vector3.UnitY));
        }

        [Test]
        public void RefusesNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientStreaks(_localId, null, Send, _logs.Add));
            Assert.Throws<ArgumentNullException>(() => new ClientStreaks(_localId, Spec(), null, _logs.Add));
            Assert.Throws<ArgumentNullException>(() => new ClientStreaks(_localId, Spec(), Send, null));
            Assert.Throws<ArgumentNullException>(() => _streaks.Apply(null));
            Assert.Throws<ArgumentNullException>(() => _streaks.Handle(null));
        }

        private static StreakSpec Spec()
        {
            return new StreakSpec(new[]
            {
                new StreakReward(StreakSpec.UavRewardId, StreakSpec.DefaultUavNameKey, 2),
                new StreakReward(StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, 4),
            });
        }

        private static SnapshotMessage Snapshot(GameModeState state, int kills, bool includeLocal = true)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = state;
            if (includeLocal)
            {
                ref var own = ref snapshot.Players.Add();
                own.PlayerId = _localId;
                own.Team = Team.Alpha;
                own.StreakKills = kills;
            }

            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _otherId;
            other.Team = Team.Bravo;
            other.StreakKills = 7;
            return snapshot;
        }

        private static StreakActivatedMessage Activated(int playerId, Team team, string rewardId, float seconds)
        {
            return new StreakActivatedMessage { PlayerId = playerId, Team = team, RewardId = rewardId, DurationSeconds = seconds };
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            var request = (StreakUseRequestMessage)message;
            _sent.Add(new StreakUseRequestMessage { RewardId = request.RewardId, Direction = request.Direction });
            return true;
        }
    }
}
