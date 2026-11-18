using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2SpectatePartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;
        private const int _allyId = 2;
        private const int _health = 100;

        private readonly List<string> _logs = new List<string>();
        private readonly List<SpectateStep> _sent = new List<SpectateStep>();
        private MatchClient _client;
        private NetV2SpectatePart _part;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Spectate.Clear();
            _logs.Clear();
            _sent.Clear();
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2SpectatePart(_client, Send, () => null);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            _client.Dispose();
            GameSignals.Spectate.Clear();
        }

        [Test]
        public void WithSpectatingOnTheFallenPlayerWatchesAnAllyAndCanStep()
        {
            _part.Apply(Snapshot(Spectating.On, ownHealth: 0));
            _part.Tick(0.1f);

            Assert.That(_part.Spectate, Is.Not.Null);
            Assert.That(GameSignals.Spectate.Value, Is.EqualTo((SpectateStatus?)new SpectateStatus(_allyId, 1, SpectateReason.Dead)));

            Assert.That(_part.Spectate.Step(SpectateStep.Next), Is.True);
            Assert.That(_sent, Is.EqualTo(new[] { SpectateStep.Next }));
        }

        [Test]
        public void WithSpectatingOffAFallenPlayerSendsNothingAndShowsNothing()
        {
            _part.Apply(Snapshot(Spectating.Off, ownHealth: 0));
            _part.Tick(0.1f);
            _part.Tick(0.1f);

            Assert.That(_part.Spectate, Is.Null);
            Assert.That(GameSignals.Spectate.HasValue, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void WithSpectatingOffTheWaitBetweenRoundsIsNotWatchedEither()
        {
            var snapshot = Snapshot(Spectating.Off, ownHealth: _health);
            snapshot.Match.HasRound = true;
            snapshot.Match.Round = RoundPhase.Preparing;

            _part.Apply(snapshot);
            _part.Tick(0.1f);

            Assert.That(_part.Spectate, Is.Null);
            Assert.That(GameSignals.Spectate.HasValue, Is.False);
        }

        [Test]
        public void TurningSpectatingOffWhileWatchingTakesTheSignalDown()
        {
            _part.Apply(Snapshot(Spectating.On, ownHealth: 0));
            _part.Tick(0.1f);
            Assert.That(GameSignals.Spectate.HasValue, Is.True);

            _part.Apply(Snapshot(Spectating.Off, ownHealth: 0));
            _part.Tick(0.1f);

            Assert.That(_part.Spectate, Is.Null);
            Assert.That(GameSignals.Spectate.HasValue, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void TurningSpectatingBackOnWatchesAgain()
        {
            _part.Apply(Snapshot(Spectating.Off, ownHealth: 0));
            _part.Tick(0.1f);

            _part.Apply(Snapshot(Spectating.On, ownHealth: 0));
            _part.Tick(0.1f);

            Assert.That(_part.Spectate, Is.Not.Null);
            Assert.That(GameSignals.Spectate.Value, Is.EqualTo((SpectateStatus?)new SpectateStatus(_allyId, 1, SpectateReason.Dead)));
        }

        [Test]
        public void TurningSpectatingOffLeavesAnotherPublishersSignal()
        {
            var other = new SpectateStatus(9, 3, SpectateReason.BetweenRounds);
            GameSignals.Spectate.Publish(other);

            _part.Apply(Snapshot(Spectating.Off, ownHealth: 0));
            _part.Tick(0.1f);

            Assert.That(GameSignals.Spectate.Value, Is.EqualTo((SpectateStatus?)other));
        }

        private static SnapshotMessage Snapshot(Spectating spectating, int ownHealth)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            snapshot.Match.State = GameModeState.Playing;
            var rules = MatchRulesSync.From(GameRules.Default);
            rules.DeathCamera = DeathCamera.Ally;
            rules.Spectating = spectating;
            snapshot.Match.Rules = rules;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.Health = ownHealth;
            ref var ally = ref snapshot.Players.Add();
            ally.PlayerId = _allyId;
            ally.Team = Team.Alpha;
            ally.Health = _health;
            return snapshot;
        }

        private bool Send(IMessage message)
        {
            Assert.That(message, Is.InstanceOf<SpectateRequestMessage>());
            _sent.Add(((SpectateRequestMessage)message).Step);
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
