namespace Shooter.Core
{
    public readonly struct ModeOutcome
    {
        public static ModeOutcome Undecided => default;

        public static ModeOutcome Draw => new ModeOutcome(true, null);

        public static ModeOutcome Win(Side side) => new ModeOutcome(true, side);

        public static ModeOutcome Decided(Side? winner) => new ModeOutcome(true, winner);

        public bool IsDecided { get; }

        public Side? Winner { get; }

        private ModeOutcome(bool isDecided, Side? winner)
        {
            IsDecided = isDecided;
            Winner = winner;
        }
    }
}
