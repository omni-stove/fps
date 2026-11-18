using System;

namespace Shooter.Core
{
    public readonly struct ReplayRecord
    {
        public ReplayRecord(int tick, ReplayRecordKind kind, ulong connection, byte channel, ArraySegment<byte> payload)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must not be negative");
            }

            if (!Enum.IsDefined(typeof(ReplayRecordKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a defined ReplayRecordKind");
            }

            if (connection == 0)
            {
                throw new ArgumentException("a record needs a connection", nameof(connection));
            }

            Tick = tick;
            Kind = kind;
            Connection = connection;
            Channel = channel;
            Payload = payload.Array == null ? new ArraySegment<byte>(Array.Empty<byte>()) : payload;
        }

        public int Tick { get; }

        public ReplayRecordKind Kind { get; }

        public ulong Connection { get; }

        public byte Channel { get; }

        // ReplayReader の読み込み用の配列を指すので、次に ReplayReader.TryRead を呼ぶまでしか中身を保証しない。
        // Disconnected では切れた理由を UTF-8 で持つ。
        public ArraySegment<byte> Payload { get; }
    }
}
