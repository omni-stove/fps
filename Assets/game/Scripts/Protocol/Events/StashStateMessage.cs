using System;

namespace Shooter.Protocol
{
    // スナップショットに載せないのは、持ち主にだけ送り、変わるのが拾う・脱出するときだけなので毎 tick の差分に要らないため。
    // 差分ではなくいつも全部を送るのは、入り直した人にも同じ 1 通で済むため (InventoryStateMessage と同じ)。
    public sealed class StashStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        private readonly StashItemList _stash = new StashItemList("stash");
        private readonly StashItemList _carried = new StashItemList("carried");
        private readonly StashItemList _broughtIn = new StashItemList("brought in");

        public ushort Id => MessageIds.StashState;

        public byte Version => CurrentVersion;

        // 試合の外に残してある物
        public StashItemList Stash => _stash;

        // 今持っている物
        public StashItemList Carried => _carried;

        // 出撃のときに持ち物庫から持ち込んだ物
        public StashItemList BroughtIn => _broughtIn;

        public ExtractionOutcome Outcome { get; set; }

        public void Clear()
        {
            _stash.Clear();
            _carried.Clear();
            _broughtIn.Clear();
            Outcome = ExtractionOutcome.None;
        }

        public void Write(MessageWriter writer)
        {
            if ((uint)Outcome >= (uint)EnumRange<ExtractionOutcome>.Count)
            {
                throw new InvalidOperationException("unknown extraction outcome " + (int)Outcome);
            }

            _stash.Write(writer);
            _carried.Write(writer);
            _broughtIn.Write(writer);
            writer.WriteByte((byte)Outcome);
        }

        public void Read(MessageReader reader)
        {
            _stash.Read(reader);
            _carried.Read(reader);
            _broughtIn.Read(reader);
            Outcome = (ExtractionOutcome)reader.ReadByteBelow(EnumRange<ExtractionOutcome>.Count, "extraction outcome");
        }
    }
}
