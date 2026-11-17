namespace Shooter.Protocol
{
    // SnapshotList が型引数の制約で呼ぶので、構造体のまま (箱詰めせずに) 呼ばれる。
    public interface ISnapshotEntity<T>
        where T : struct
    {
        int EntityId { get; }

        int PresentBits { get; }

        int AllBits { get; }

        int OptionalBits { get; }

        int ChangedBits(in T baseline);

        void WriteBits(MessageWriter writer, int bits);

        void ReadBits(MessageReader reader, int id, int bits);

        void FillFrom(in T baseline);
    }
}
