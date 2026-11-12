namespace Fps.Core
{
    /// <summary><see cref="PacketReassembler.Read"/> の結果。</summary>
    public enum PacketReadResult
    {
        /// <summary>メッセージが揃った。</summary>
        Complete,

        /// <summary>断片を受け取ったが、まだ揃っていない。</summary>
        Incomplete,

        /// <summary>形式が壊れている・上限を超えているので捨てた。</summary>
        Malformed,
    }
}
