namespace Fps.Core
{
    public sealed class CycleSpec
    {
        public CycleSpec(float cycleSeconds, bool leavesAds = false)
        {
            CycleSeconds = SpecGuard.PositiveFinite(cycleSeconds, nameof(cycleSeconds));
            LeavesAds = leavesAds;
        }

        public float CycleSeconds { get; }

        public bool LeavesAds { get; }
    }
}
