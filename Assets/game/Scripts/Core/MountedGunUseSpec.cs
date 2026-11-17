namespace Fps.Core
{
    public sealed class MountedGunUseSpec
    {
        public const float DefaultSeekRadius = 8f;

        public const float DefaultGiveUpSeconds = 3f;

        public const float DefaultRemountDelaySeconds = 5f;

        public static readonly MountedGunUseSpec Default = new MountedGunUseSpec();

        public MountedGunUseSpec(
            float seekRadius = DefaultSeekRadius,
            float giveUpSeconds = DefaultGiveUpSeconds,
            float remountDelaySeconds = DefaultRemountDelaySeconds)
        {
            SeekRadius = SpecGuard.NonNegativeFinite(seekRadius, nameof(seekRadius));
            GiveUpSeconds = SpecGuard.PositiveFinite(giveUpSeconds, nameof(giveUpSeconds));
            RemountDelaySeconds = SpecGuard.NonNegativeFinite(remountDelaySeconds, nameof(remountDelaySeconds));
        }

        public float SeekRadius { get; }

        public float GiveUpSeconds { get; }

        public float RemountDelaySeconds { get; }
    }
}
