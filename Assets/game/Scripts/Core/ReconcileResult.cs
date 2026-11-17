namespace Shooter.Core
{
    public readonly struct ReconcileResult
    {
        public ReconcileOutcome Outcome { get; }

        public int Tick { get; }

        public float Error { get; }

        public int ResimulatedTicks { get; }

        public ReconcileResult(ReconcileOutcome outcome, int tick, float error, int resimulatedTicks)
        {
            Outcome = outcome;
            Tick = tick;
            Error = error;
            ResimulatedTicks = resimulatedTicks;
        }
    }
}
