namespace Shooter.Core
{
    public readonly struct TimeScaleRequest
    {
        public TimeScaleRequest(string reason, float scale, float remainingSeconds)
        {
            Reason = reason;
            Scale = scale;
            RemainingSeconds = remainingSeconds;
        }

        public string Reason { get; }

        public float Scale { get; }

        public float RemainingSeconds { get; }

        public override string ToString()
        {
            return TimeScaleArbitration.FormatRequest(this);
        }
    }
}
