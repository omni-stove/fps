namespace Fps.Server
{
    /// <summary>
    /// サーバーが 1 人のプレイヤーについて数える発射番号 (Unity の <c>ServerShotSequence</c> と同じ数え方)。検査を通って弾を消費した射撃にだけ
    /// <see cref="Assign"/> で番号を付け、ばらつきの種にはクライアントの番号ではなくこの番号を使う。
    /// 訂正が届くまでに古い世代で届く弾は同じだけずれていて、持ち主は訂正でまとめて合わせるので、世代が今と違う弾では訂正を重ねない。
    /// </summary>
    internal sealed class ServerShotSequence
    {
        public int NextIndex { get; private set; }

        public int Generation { get; private set; }

        public ShotNumbering Assign(int clientIndex, int clientGeneration)
        {
            var index = NextIndex;
            NextIndex = unchecked(index + 1);
            if (clientIndex == index || clientGeneration != Generation)
            {
                return new ShotNumbering(index, false, Generation);
            }

            Generation = unchecked(Generation + 1);
            return new ShotNumbering(index, true, Generation);
        }
    }

    internal readonly struct ShotNumbering
    {
        public ShotNumbering(int index, bool needsCorrection, int generation)
        {
            Index = index;
            NeedsCorrection = needsCorrection;
            Generation = generation;
        }

        public int Index { get; }

        public bool NeedsCorrection { get; }

        public int Generation { get; }
    }
}
