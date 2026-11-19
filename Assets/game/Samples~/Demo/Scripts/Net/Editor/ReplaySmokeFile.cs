using System;
using System.Collections.Generic;
using System.IO;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Editor
{
    // 本物のサーバーを立てずに再生を通しで見るため、承認・切断だけの試合を ReplayRecorder に流して、記録したときと同じ形のファイルを作る。
    public static class ReplaySmokeFile
    {
        public const int PlayerId = 1;

        public const int TickRate = 30;

        public const int MaxHealth = 100;

        public const float AcceptedAtSeconds = 0.1f;

        public const string EndReason = "server closed";

        public static readonly ConnectionId Server = new ConnectionId(1);

        public static byte[] Build(string version, string map, float seconds, DateTime recordedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("version must not be empty", nameof(version));
            }

            if (string.IsNullOrWhiteSpace(map))
            {
                throw new ArgumentException("map must not be empty", nameof(map));
            }

            if (!(seconds > AcceptedAtSeconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "the replay must last longer than " + AcceptedAtSeconds + "s");
            }

            var inner = new ScriptedTransport();
            var output = new MemoryStream();
            var now = 0d;
            var recorder = new ReplayRecorder(inner, output, version, _ => { }, () => now, () => recordedAtUtc);
            try
            {
                recorder.Connect(string.Empty, 0);
                Receive(recorder, inner, TransportEvent.Connected(Server));

                now = AcceptedAtSeconds;
                Receive(recorder, inner, TransportEvent.Data(Server, TransportChannel.Reliable, new ArraySegment<byte>(Accepted(version, map))));

                now = seconds;
                Receive(recorder, inner, TransportEvent.Disconnected(Server, EndReason));
                if (!recorder.HasRecording)
                {
                    throw new InvalidOperationException("the recorder kept nothing of the scripted match");
                }
            }
            finally
            {
                recorder.Dispose();
            }

            return output.ToArray();
        }

        public static byte[] Accepted(string version, string map)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage
            {
                PlayerId = PlayerId,
                ServerVersion = version,
                TickRate = TickRate,
                ServerTick = 0,
                Map = map,
                MaxHealth = MaxHealth,
            });
            return writer.ToArray();
        }

        private static void Receive(ReplayRecorder recorder, ScriptedTransport inner, TransportEvent transportEvent)
        {
            inner.Enqueue(transportEvent);
            if (!recorder.TryReceive(out _))
            {
                throw new InvalidOperationException("the recorder did not pass the scripted event through");
            }
        }

        private sealed class ScriptedTransport : IMatchTransport
        {
            private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();

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
                milliseconds = 0;
                return false;
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
    }
}
