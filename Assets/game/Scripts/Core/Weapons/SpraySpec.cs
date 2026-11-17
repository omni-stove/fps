namespace Shooter.Core
{
    public sealed class SpraySpec
    {
        public SpraySpec(float tickSeconds, float coneDegrees, string visualId = null)
        {
            TickSeconds = SpecGuard.PositiveFinite(tickSeconds, nameof(tickSeconds));
            ConeDegrees = SpecGuard.Angle(coneDegrees, nameof(coneDegrees));
            VisualId = SpecGuard.ReferenceId(visualId);
        }

        public float TickSeconds { get; }

        public float ConeDegrees { get; }

        public string VisualId { get; }
    }
}
