namespace Shooter.Server
{
    // 数え方は Unity の ServerShotSequence と同じにする。
    // 訂正が届くまでに古い世代で届く弾は同じだけずれていて、持ち主は訂正でまとめて合わせるので、世代が今と違う弾では訂正を重ねない。
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
