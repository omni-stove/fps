using System;
using System.Globalization;

namespace Fps.Protocol
{
    /// <summary>
    /// クライアントが毎 tick 送る移動の入力。落ちてもよい届け方で送れるよう、まだサーバーが処理したと分からない直近の tick を
    /// 続けて <see cref="MaxFrames"/> 個まで載せる。最後に受け取ったスナップショットの tick も載せ、サーバーが差分の基準にする。
    /// </summary>
    public sealed class InputMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        /// <summary>1 つに載せる tick の最大数。</summary>
        public const int MaxFrames = 16;

        /// <summary>まだスナップショットを受け取っていないことを表す <see cref="AckedSnapshotTick"/>。</summary>
        public const int NoSnapshot = -1;

        private readonly InputFrame[] _frames = new InputFrame[MaxFrames];

        public ushort Id => MessageIds.Input;

        public byte Version => CurrentVersion;

        /// <summary>クライアントが最後に受け取って組み立てられたスナップショットの tick。無ければ <see cref="NoSnapshot"/>。</summary>
        public int AckedSnapshotTick { get; set; } = NoSnapshot;

        /// <summary>載せている tick の数。</summary>
        public int Count { get; private set; }

        /// <summary>古い順の index 番目の tick の入力。</summary>
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

        /// <summary>frame を一番新しい tick として足す。</summary>
        /// <exception cref="InvalidOperationException">もう <see cref="MaxFrames"/> 個ある、または前の tick の次でない。</exception>
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
                writer.WriteByte((byte)_frames[i].Buttons);
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
                var buttons = (InputButtons)reader.ReadByteBelow((int)InputButtons.All + 1, "input buttons");
                _frames[i] = new InputFrame(firstTick + i, move, yaw, buttons);
            }

            Count = count;
        }
    }
}
