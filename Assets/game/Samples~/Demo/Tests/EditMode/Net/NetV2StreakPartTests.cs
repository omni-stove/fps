using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2StreakPartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;

        private readonly List<string> _logs = new List<string>();
        private readonly List<string> _sentRewards = new List<string>();
        private readonly List<StreakUseRequestMessage> _sent = new List<StreakUseRequestMessage>();
        private MatchClient _client;
        private NetV2StreakPart _part;
        private Streaks _rule;
        private bool _pressed;
        private StreakTargeting _targeting;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Streak.Clear();
            GameSignals.StreakTargeting.Clear();
            _logs.Clear();
            _sentRewards.Clear();
            _sent.Clear();
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
            GameSignals.StreakTargeting.Clear();

            // Edit Mode では OnDestroy が呼ばれず、StreakTargeting が作った見下ろすカメラと印が残るので、ここで消す
            if (_targeting != null)
            {
                if (_targeting.ViewCamera != null)
                {
                    Object.DestroyImmediate(_targeting.ViewCamera.gameObject);
                }

                if (_targeting.Marker != null)
                {
                    Object.DestroyImmediate(_targeting.Marker);
                }

                Object.DestroyImmediate(_targeting.gameObject);
                _targeting = null;
            }
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
        public void PressingUseForTheAirstrikeOpensTheMapAndSendsTheChosenSpotOnConfirm()
        {
            var part = AirstrikePart();
            part.Apply(Snapshot(kills: 1));
            _pressed = true;

            part.Tick(0.1f);

            Assert.That(part.IsTargeting, Is.True);
            Assert.That(_sent, Is.Empty, "nothing is sent while choosing");
            Assert.That(GameSignals.StreakTargeting.Value.HasValue, Is.True);
            Assert.That(GameSignals.StreakTargeting.Value.Value.RewardId, Is.EqualTo(StreakSpec.AirstrikeRewardId));

            part.Tick(0.1f);
            Assert.That(_sent, Is.Empty, "the held use button does not reopen or send while the map is open");

            _targeting.Step(new StreakTargetingInput(new Vector2(1f, 0f), 45f, false, false), 0.5f);
            _targeting.Step(new StreakTargetingInput(Vector2.zero, 0f, true, false), 0f);

            Assert.That(part.IsTargeting, Is.False);
            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].RewardId, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(_sent[0].HasTarget, Is.True);
            Assert.That(_sent[0].Target, Is.EqualTo(new System.Numerics.Vector3(20f, 0f, 0f)), "40 m/s east for half a second from the body");
            Assert.That(_sent[0].HeadingDegrees, Is.EqualTo(45f));
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1), "the slot waits for the server");
            part.Dispose();
        }

        [Test]
        public void CancellingTheMapSendsNothingAndKeepsTheReward()
        {
            var part = AirstrikePart();
            part.Apply(Snapshot(kills: 1));
            _pressed = true;
            part.Tick(0.1f);

            _targeting.Step(new StreakTargetingInput(Vector2.zero, 0f, false, true), 0.1f);

            Assert.That(part.IsTargeting, Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(GameSignals.StreakTargeting.HasValue, Is.False);
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1));
            part.Dispose();
        }

        [Test]
        public void FallingWhileChoosingClosesTheMap()
        {
            var part = AirstrikePart();
            part.Apply(Snapshot(kills: 1));
            _pressed = true;
            part.Tick(0.1f);
            Assert.That(part.IsTargeting, Is.True);

            LogAssert.Expect(LogType.Log, StreakLog.TargetingCancelled(ClientStreakLog.Id(_localId), StreakSpec.AirstrikeRewardId, StreakTargeting.CancelledByDeath));
            part.Apply(Snapshot(kills: 0, health: 0));

            Assert.That(part.IsTargeting, Is.False);
            Assert.That(_sent, Is.Empty);
            part.Dispose();
        }

        [Test]
        public void WithoutABodyToChooseFromTheAirstrikeIsNotSent()
        {
            var part = new NetV2StreakPart(_client, SendTargeted, () => Streaks.On, _logs.Add, AirstrikeOnly(), () => null, () => _pressed, () => null);
            part.Apply(Snapshot(kills: 1));
            _pressed = true;

            part.Tick(0.1f);

            Assert.That(part.IsTargeting, Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(StreakLog.RewardUnavailable(ClientStreakLog.Id(_localId), StreakSpec.AirstrikeRewardId, nameof(StreakTargeting))));
            part.Dispose();
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

        private static StreakSpec AirstrikeOnly()
        {
            return new StreakSpec(new[] { new StreakReward(StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, 1) });
        }

        // 選ぶ部品は体の無い空の物に付け、体の位置 (原点) から選ばせる
        private NetV2StreakPart AirstrikePart()
        {
            _targeting = new GameObject("StreakPartTests.Targeting").AddComponent<StreakTargeting>();
            return new NetV2StreakPart(_client, SendTargeted, () => Streaks.On, _logs.Add, AirstrikeOnly(), () => null, () => _pressed, () => _targeting);
        }

        private static SnapshotMessage Snapshot(int kills, int health = 100)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.StreakKills = kills;
            own.Health = health;
            return snapshot;
        }

        private bool SendTargeted(IMessage message)
        {
            var request = (StreakUseRequestMessage)message;
            _sent.Add(new StreakUseRequestMessage
            {
                RewardId = request.RewardId,
                HasTarget = request.HasTarget,
                Target = request.Target,
                HeadingDegrees = request.HeadingDegrees,
            });
            return true;
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
