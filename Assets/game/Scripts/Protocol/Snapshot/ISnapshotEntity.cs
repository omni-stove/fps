namespace Fps.Protocol
{
    /// <summary>
    /// スナップショットに並ぶ 1 つの物。中身をいくつかの部分 (ビット) に分け、前のスナップショットの同じ <see cref="EntityId"/> の物から
    /// 変わった部分だけを送れるようにする。
    /// </summary>
    /// <remarks>
    /// <see cref="SnapshotList{T}"/> が型引数の制約で呼ぶので、構造体のまま (箱詰めせずに) 呼ばれる。
    /// </remarks>
    public interface ISnapshotEntity<T>
        where T : struct
    {
        int EntityId { get; }

        /// <summary>中身が入っている部分。読んだ直後は届いた部分だけで、基準から埋めると全部になる。</summary>
        int PresentBits { get; }

        /// <summary>全部の部分。</summary>
        int AllBits { get; }

        /// <summary>baseline から変わった部分。</summary>
        int ChangedBits(in T baseline);

        void WriteBits(MessageWriter writer, int bits);

        /// <summary>Id を id に、<see cref="PresentBits"/> を bits にして、bits の部分を読む。他の部分は前の中身のまま残す。</summary>
        void ReadBits(MessageReader reader, int id, int bits);

        /// <summary>届かなかった部分を baseline から写し、<see cref="PresentBits"/> を全部にする。</summary>
        void FillFrom(in T baseline);
    }
}
