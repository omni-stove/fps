using System;
using System.Globalization;

namespace Shooter.Protocol
{
    // 落ちてもよい届け方で送れるよう、まだサーバーが処理したと分からない直近の tick を続けて MaxFrames 個まで載せる。
    public sealed class InputMessage : IMessage
    {
        public const byte CurrentVersion = 4;

        public const int MaxFrames = 16;

        public const int NoSnapshot = -1;

        private readonly InputFrame[] _frames = new InputFrame[MaxFrames];

        public ushort Id => MessageIds.Input;

        public byte Version => CurrentVersion;

        public int AckedSnapshotTick { get; set; } = NoSnapshot;

        public int Count { get; private set; }

        public InputFrame this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Count);
                }

                return _frames[index];
            }
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Add(InputFrame frame)
        {
            if (Count == MaxFrames)
            {
                throw new InvalidOperationException("an input message holds at most " + MaxFrames + " ticks");
            }

            if (frame.Tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Tick, "tick must not be negative");
            }

            if (Count > 0 && frame.Tick != _frames[Count - 1].Tick + 1)
            {
                throw new InvalidOperationException(
                    "tick " + frame.Tick.ToString(CultureInfo.InvariantCulture) + " does not follow tick "
                    + _frames[Count - 1].Tick.ToString(CultureInfo.InvariantCulture));
            }

            _frames[Count] = frame;
            Count++;
        }

        public void Write(MessageWriter writer)
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("an input message needs at least one tick");
            }

            writer.WriteInt32(AckedSnapshotTick);
            writer.WriteByte((byte)Count);
            writer.WriteInt32(_frames[0].Tick);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteVector2(_frames[i].Move);
                writer.WriteSingle(_frames[i].Yaw);
                writer.WriteUInt16((ushort)_frames[i].Buttons);
            }
        }

        public void Read(MessageReader reader)
        {
            Count = 0;
            AckedSnapshotTick = reader.ReadInt32AtLeast(NoSnapshot, "acked snapshot tick");
            var count = reader.ReadByteBelow(MaxFrames + 1, "input tick count");
            if (count == 0)
            {
                throw new MessageReadException("an input message carries no ticks");
            }

            var firstTick = reader.ReadInt32AtLeast(0, "first input tick");
            if (firstTick > int.MaxValue - count)
            {
                throw new MessageReadException("input ticks from " + firstTick.ToString(CultureInfo.InvariantCulture) + " overflow");
            }

            for (var i = 0; i < count; i++)
            {
                var move = reader.ReadFiniteVector2("move input");
                var yaw = reader.ReadFiniteSingle("yaw");
                var buttons = (InputButtons)reader.ReadUInt16();
                if ((buttons & ~InputButtons.All) != 0)
                {
                    throw new MessageReadException(
                        "input buttons " + ((int)buttons).ToString(CultureInfo.InvariantCulture) + " include an unknown button");
                }

                _frames[i] = new InputFrame(firstTick + i, move, yaw, buttons);
            }

            Count = count;
        }
    }
}
