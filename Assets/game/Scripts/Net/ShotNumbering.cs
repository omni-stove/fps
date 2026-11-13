namespace Fps.Net
{
    /// <summary><see cref="ServerShotSequence.Assign"/> が弾 1 発に付けた番号と、持ち主へ訂正を送るか。</summary>
    public readonly struct ShotNumbering
    {
        public ShotNumbering(int index, bool needsCorrection, int generation)
        {
            Index = index;
            NeedsCorrection = needsCorrection;
            Generation = generation;
        }

        /// <summary>サーバーが付けた発射番号。ばらつきの種に使う。</summary>
        public int Index { get; }

        /// <summary>クライアントの番号と食い違ったので、持ち主へ訂正を送るか。</summary>
        public bool NeedsCorrection { get; }

        /// <summary>番号を付けた後の訂正の世代。訂正を送るならその訂正の世代。</summary>
        public int Generation { get; }
    }
}
