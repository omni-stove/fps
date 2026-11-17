namespace Shooter.Core
{
    public sealed class BeamSpec
    {
        public BeamSpec(float tickSeconds, string visualId = null)
        {
            TickSeconds = SpecGuard.PositiveFinite(tickSeconds, nameof(tickSeconds));
            VisualId = SpecGuard.ReferenceId(visualId);
        }

        public float TickSeconds { get; }

        public string VisualId { get; }
    }
}
