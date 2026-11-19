namespace Shooter.Core
{
    public sealed class ReplaySpec
    {
        public const float DefaultFreeSpeed = DeathCamFraming.DefaultFreeSpeed;

        public static ReplaySpec Default { get; } = new ReplaySpec();

        // 再生の自由カメラを倒し切ったときの速さ (m/s)
        public float FreeSpeed { get; }

        public ReplaySpec(float freeSpeed = DefaultFreeSpeed)
        {
            FreeSpeed = SpecGuard.NonNegativeFinite(freeSpeed, nameof(freeSpeed));
        }
    }
}
