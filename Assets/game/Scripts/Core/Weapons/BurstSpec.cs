namespace Shooter.Core
{
    public sealed class BurstSpec
    {
        // 1 発のバーストは Semi と変わらない。
        public const int MinRoundsPerBurst = 2;

        public BurstSpec(int roundsPerBurst, float roundIntervalSeconds)
        {
            RoundsPerBurst = SpecGuard.AtLeast(roundsPerBurst, MinRoundsPerBurst, nameof(roundsPerBurst));
            RoundIntervalSeconds = SpecGuard.NonNegativeFinite(roundIntervalSeconds, nameof(roundIntervalSeconds));
        }

        public int RoundsPerBurst { get; }

        public float RoundIntervalSeconds { get; }
    }
}
