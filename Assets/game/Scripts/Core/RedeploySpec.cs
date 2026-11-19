namespace Shooter.Core
{
    public sealed class RedeploySpec
    {
        public const int DefaultCharges = 1;

        public const int DefaultLastPhaseIndex = 1;

        public const float DefaultDelaySeconds = 5f;

        public static RedeploySpec Default { get; } = new RedeploySpec();

        public static RedeploySpec None { get; } = new RedeploySpec(charges: 0);

        public RedeploySpec(
            int charges = DefaultCharges,
            int lastPhaseIndex = DefaultLastPhaseIndex,
            float delaySeconds = DefaultDelaySeconds)
        {
            Charges = SpecGuard.NonNegative(charges, nameof(charges));
            LastPhaseIndex = SpecGuard.NonNegative(lastPhaseIndex, nameof(lastPhaseIndex));
            DelaySeconds = SpecGuard.NonNegativeFinite(delaySeconds, nameof(delaySeconds));
        }

        public int Charges { get; }

        public int LastPhaseIndex { get; }

        public float DelaySeconds { get; }

        public bool IsEnabled => Charges > 0;
    }
}
