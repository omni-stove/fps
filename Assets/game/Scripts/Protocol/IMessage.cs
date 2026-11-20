namespace Shooter.Protocol
{
    // 毎 tick 届くものでも GC を起こさないよう、受け手は種類ごとに 1 つのインスタンスを使い回して Read で上書きする
    // (MessageTable)。
    public interface IMessage
    {
        ushort Id { get; }

        // 中身の形を変えたら上げる。違う版のメッセージは、IReadsOlderVersions で読める前の版のほかは読まずに断る。
        byte Version { get; }

        void Write(MessageWriter writer);

        void Read(MessageReader reader);
    }
}
