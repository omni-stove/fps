namespace Shooter.Core
{
    public sealed class AssistSpec
    {
        public const float DefaultWindowSeconds = 10f;

        // 体力 100 の 1/5。かすった 1 発ではアシストにならず、撃ち合いに加わった程度で届く値にする。
        public const int DefaultMinDamage = 20;

        public static AssistSpec Default { get; } = new AssistSpec();

        public float WindowSeconds { get; }

        public int MinDamage { get; }

        public AssistSpec(float windowSeconds = DefaultWindowSeconds, int minDamage = DefaultMinDamage)
        {
            WindowSeconds = SpecGuard.PositiveFinite(windowSeconds, nameof(windowSeconds));
            MinDamage = SpecGuard.AtLeast(minDamage, 1, nameof(minDamage));
        }

        public bool InWindow(double hitTime, double now)
        {
            return now - hitTime <= WindowSeconds;
        }
    }
}
