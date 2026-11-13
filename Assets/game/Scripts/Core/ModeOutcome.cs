namespace Fps.Core
{
    /// <summary><see cref="IWinCondition"/> が調べた結果。決着がついたか、ついたなら勝った陣営 (引き分けなら null)。</summary>
    public readonly struct ModeOutcome
    {
        /// <summary>まだ決着していない。</summary>
        public static ModeOutcome Undecided => default;

        /// <summary>引き分けで決着した。</summary>
        public static ModeOutcome Draw => new ModeOutcome(true, null);

        /// <summary>side の勝ちで決着した。</summary>
        public static ModeOutcome Win(Side side) => new ModeOutcome(true, side);

        /// <summary>winner の勝ちで決着した。null なら引き分け。</summary>
        public static ModeOutcome Decided(Side? winner) => new ModeOutcome(true, winner);

        /// <summary>決着したら true。</summary>
        public bool IsDecided { get; }

        /// <summary>勝った陣営。決着していないときと引き分けのときは null。</summary>
        public Side? Winner { get; }

        private ModeOutcome(bool isDecided, Side? winner)
        {
            IsDecided = isDecided;
            Winner = winner;
        }
    }
}
