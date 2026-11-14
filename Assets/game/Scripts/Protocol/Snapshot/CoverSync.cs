using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// プレイヤーのカバーの状態 (Netcode の <c>NetCoverState</c> の中身)。予測で巻き戻せるよう、<see cref="CoverSnapshot"/> に
    /// 見た目に使う縁・上端の高さ・実際に出した覗き量を足したもの。
    /// </summary>
    public struct CoverSync
    {
        public CoverSnapshot Snapshot { get; set; }

        /// <summary>張り付いている物の上端のワールドの高さ。</summary>
        public float TopY { get; set; }

        /// <summary>覗ける縁。</summary>
        public CoverEdge Edges { get; set; }

        /// <summary>実際に出した覗き量。</summary>
        public float AppliedPeek { get; set; }

        public static CoverSync Out => default;

        public CoverSync(CoverSnapshot snapshot, float topY, CoverEdge edges, float appliedPeek)
        {
            Snapshot = snapshot;
            TopY = topY;
            Edges = edges;
            AppliedPeek = appliedPeek;
        }

        public bool SameAs(in CoverSync other)
        {
            var a = Snapshot;
            var b = other.Snapshot;
            return a.Phase == b.Phase
                && a.Height == b.Height
                && Same.Bits(a.Normal, b.Normal)
                && Same.Bits(a.PeekOffset, b.PeekOffset)
                && Same.Bits(a.EnterElapsed, b.EnterElapsed)
                && Same.Bits(a.BackElapsed, b.BackElapsed)
                && Same.Bits(a.PreferredSide, b.PreferredSide)
                && Same.Bits(TopY, other.TopY)
                && Edges == other.Edges
                && Same.Bits(AppliedPeek, other.AppliedPeek);
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Snapshot.Phase);
            writer.WriteByte((byte)Snapshot.Height);
            writer.WriteByte((byte)Edges);
            writer.WriteVector3(Snapshot.Normal);
            writer.WriteSingle(Snapshot.PeekOffset);
            writer.WriteSingle(Snapshot.EnterElapsed);
            writer.WriteSingle(Snapshot.BackElapsed);
            writer.WriteSingle(Snapshot.PreferredSide);
            writer.WriteSingle(TopY);
            writer.WriteSingle(AppliedPeek);
        }

        internal static CoverSync Read(MessageReader reader)
        {
            var phase = (CoverPhase)reader.ReadByteBelow(EnumRange<CoverPhase>.Count, "cover phase");
            var height = (CoverHeight)reader.ReadByteBelow(EnumRange<CoverHeight>.Count, "cover height");
            var edges = (CoverEdge)reader.ReadByteBelow((int)(CoverEdge.Left | CoverEdge.Right) + 1, "cover edges");
            var normal = reader.ReadFiniteVector3("cover normal");
            var peekOffset = reader.ReadFiniteSingle("cover peek offset");
            var enterElapsed = reader.ReadFiniteSingle("cover enter time");
            var backElapsed = reader.ReadFiniteSingle("cover back time");
            var preferredSide = reader.ReadFiniteSingle("cover side");
            var topY = reader.ReadFiniteSingle("cover top");
            var appliedPeek = reader.ReadFiniteSingle("cover applied peek");
            return new CoverSync(
                new CoverSnapshot(phase, height, normal, peekOffset, enterElapsed, backElapsed, preferredSide), topY, edges, appliedPeek);
        }
    }
}
