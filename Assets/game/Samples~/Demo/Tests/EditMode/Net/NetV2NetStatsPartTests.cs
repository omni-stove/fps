using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2NetStatsPartTests
    {
        private const int AcceptedTick = 100;

        private static readonly ConnectionId _server = new ConnectionId(7);

        private readonly List<string> _logs = new List<string>();
        private readonly List<NetStatsSignal?> _heard = new List<NetStatsSignal?>();
        private ScriptedTransport _transport;
        private MatchClient _client;
        private NetV2NetStatsPart _part;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _logs.Clear();
            _heard.Clear();
            GameSignals.NetStats.Changed += _heard.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _part?.Dispose();
            _client?.Dispose();
            _part = null;
            _client = null;
            GameSignals.ResetAll();
        }

        [Test]
        public void NothingIsPublishedBeforeTheServerAccepts()
        {
            Start(new ScriptedTransport());
            _client.Connect("127.0.0.1", 7777);
            _transport.Enqueue(TransportEvent.Connected(_server));
            _client.Poll();

            _part.Tick(0.1f);

            Assert.That(_client.State, Is.EqualTo(MatchClientState.Approving));
            Assert.That(GameSignals.NetStats.HasValue, Is.False);
            Assert.That(_heard, Is.Empty);
        }

        [Test]
        public void TheFirstTickAfterAcceptingPublishesThePingAndTheAcceptedTick()
        {
            Connect(new ScriptedTransport { RoundTripMs = 42 });

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value, Is.EqualTo((NetStatsSignal?)new NetStatsSignal(42, null, AcceptedTick)));
            Assert.That(_part.Published, Is.EqualTo(GameSignals.NetStats.Value));
        }

        [Test]
        public void APingTheTransportHasNotMeasuredYetIsUnknown()
        {
            Connect(new ScriptedTransport { RoundTripMs = null });

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value.Value.PingMs, Is.Null);
            Assert.That(GameSignals.NetStats.Value.Value.ServerTick, Is.EqualTo(AcceptedTick));
        }

        [Test]
        public void ATransportThatCannotCountLossLeavesTheLossUnknown()
        {
            Connect(new ScriptedTransport { RoundTripMs = 30 });

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value.Value.LossPercent, Is.Null, "unknown, not 0%");
        }

        [Test]
        public void ATransportThatCountsLossPublishesIt()
        {
            Connect(new LossTransport { RoundTripMs = 30, LossPercent = 7 });

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value, Is.EqualTo((NetStatsSignal?)new NetStatsSignal(30, 7, AcceptedTick)));
        }

        [Test]
        public void ALossTransportThatHasNotCountedYetLeavesTheLossUnknown()
        {
            Connect(new LossTransport { RoundTripMs = 30, LossPercent = null });

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value.Value.LossPercent, Is.Null);
        }

        [Test]
        public void TheLossIsReadThroughTheReplayRecorder()
        {
            var inner = new LossTransport { RoundTripMs = 30, LossPercent = 12 };
            _transport = inner;
            var recorder = new ReplayRecorder(inner, new System.IO.MemoryStream(), "0.2.0", _logs.Add);
            _client = new MatchClient(recorder, "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2NetStatsPart(_client);
            AcceptThroughTransport();

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value, Is.EqualTo((NetStatsSignal?)new NetStatsSignal(30, 12, AcceptedTick)));
        }

        [Test]
        public void OutOfRangeTransportValuesAreClamped()
        {
            Connect(new LossTransport { RoundTripMs = -5, LossPercent = 250 });

            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.Value, Is.EqualTo((NetStatsSignal?)new NetStatsSignal(0, 100, AcceptedTick)));
        }

        [Test]
        public void TheLatestSnapshotTickReplacesTheAcceptedTick()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 });
            _part.Tick(0.016f);

            _part.Apply(new SnapshotMessage { Tick = 160 });
            _part.Tick(NetV2NetStatsPart.DefaultIntervalSeconds);

            Assert.That(GameSignals.NetStats.Value.Value.ServerTick, Is.EqualTo(160));
        }

        [Test]
        public void ValuesAreReadAgainOnlyOnceTheIntervalHasPassed()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 }, 0.5f);
            _part.Tick(0.016f);
            _transport.RoundTripMs = 90;

            _part.Tick(0.25f);
            Assert.That(GameSignals.NetStats.Value.Value.PingMs, Is.EqualTo(20), "the interval has not passed yet");

            _part.Tick(0.25f);
            Assert.That(GameSignals.NetStats.Value.Value.PingMs, Is.EqualTo(90));
            Assert.That(_heard, Has.Count.EqualTo(2));
        }

        [Test]
        public void UnchangedValuesAreNotPublishedAgain()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 });

            _part.Tick(0.016f);
            _part.Tick(NetV2NetStatsPart.DefaultIntervalSeconds);
            _part.Tick(NetV2NetStatsPart.DefaultIntervalSeconds);

            Assert.That(_heard, Has.Count.EqualTo(1));
        }

        [Test]
        public void LosingTheServerEmptiesTheSignal()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 });
            _part.Tick(0.016f);

            _transport.Enqueue(TransportEvent.Disconnected(_server, "timeout"));
            _client.Poll();

            Assert.That(_client.State, Is.EqualTo(MatchClientState.Disconnected));
            Assert.That(GameSignals.NetStats.HasValue, Is.False);
            Assert.That(_part.Published, Is.Null);
            Assert.That(_heard[_heard.Count - 1], Is.Null);

            _part.Tick(NetV2NetStatsPart.DefaultIntervalSeconds);
            Assert.That(GameSignals.NetStats.HasValue, Is.False, "nothing is published after the disconnect");
        }

        [Test]
        public void LeavingEmptiesTheSignalOnTheNextTick()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 });
            _part.Tick(0.016f);

            _client.Leave();
            _part.Tick(0.016f);

            Assert.That(GameSignals.NetStats.HasValue, Is.False);
        }

        [Test]
        public void DisposingClearsOnlyWhatThePartPublished()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 });
            _part.Tick(0.016f);
            var other = new NetStatsSignal(5, 0, 1);
            GameSignals.NetStats.Publish(other);

            _part.Dispose();

            Assert.That(GameSignals.NetStats.Value, Is.EqualTo((NetStatsSignal?)other));

            GameSignals.NetStats.Clear();
            var part = new NetV2NetStatsPart(_client);
            part.Tick(0.016f);
            Assert.That(GameSignals.NetStats.HasValue, Is.True);
            part.Dispose();
            Assert.That(GameSignals.NetStats.HasValue, Is.False);
        }

        [Test]
        public void ADisposedPartPublishesNothing()
        {
            Connect(new ScriptedTransport { RoundTripMs = 20 });
            _part.Dispose();

            _part.Tick(0.016f);
            _part.Apply(new SnapshotMessage { Tick = 5 });

            Assert.That(GameSignals.NetStats.HasValue, Is.False);
            Assert.That(_part.Handle(new PingPlacedMessage()), Is.False);
        }

        [Test]
        public void RefusesMissingOrBadInputs()
        {
            var client = new MatchClient(new ScriptedTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            try
            {
                Assert.Throws<ArgumentNullException>(() => new NetV2NetStatsPart(null));
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetV2NetStatsPart(client, 0f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetV2NetStatsPart(client, -1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetV2NetStatsPart(client, float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetV2NetStatsPart(client, float.PositiveInfinity));
            }
            finally
            {
                client.Dispose();
            }
        }

        private void Start(ScriptedTransport transport, float intervalSeconds = NetV2NetStatsPart.DefaultIntervalSeconds)
        {
            _transport = transport;
            _client = new MatchClient(transport, "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2NetStatsPart(_client, intervalSeconds);
        }

        private void Connect(ScriptedTransport transport, float intervalSeconds = NetV2NetStatsPart.DefaultIntervalSeconds)
        {
            Start(transport, intervalSeconds);
            AcceptThroughTransport();
        }

        private void AcceptThroughTransport()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Enqueue(TransportEvent.Connected(_server));
            _transport.Enqueue(TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(Accepted())));
            _client.Poll();
            Assert.That(_client.State, Is.EqualTo(MatchClientState.Connected));
        }

        private static byte[] Accepted()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage
            {
                PlayerId = 3,
                ServerVersion = "0.2.0",
                TickRate = 30,
                ServerTick = AcceptedTick,
                Map = "Arena",
                MaxHealth = 100,
            });
            return writer.ToArray();
        }

        private class ScriptedTransport : IMatchTransport
        {
            private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();

            public int? RoundTripMs { get; set; }

            public bool IsActive { get; private set; }

            public void Enqueue(TransportEvent transportEvent)
            {
                _events.Enqueue(transportEvent);
            }

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
                IsActive = true;
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
                milliseconds = RoundTripMs ?? 0;
                return RoundTripMs.HasValue;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                if (_events.Count == 0)
                {
                    transportEvent = default;
                    return false;
                }

                transportEvent = _events.Dequeue();
                return true;
            }

            public void Shutdown(string reason)
            {
                IsActive = false;
            }

            public void Dispose()
            {
                IsActive = false;
            }
        }

        private sealed class LossTransport : ScriptedTransport, IConnectionLoss
        {
            public int? LossPercent { get; set; }

            public bool TryGetLossPercent(ConnectionId connection, out int percent)
            {
                percent = LossPercent ?? 0;
                return LossPercent.HasValue;
            }
        }
    }
}
