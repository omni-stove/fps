namespace Shooter.Core
{
    public sealed class ActiveStatusEffect
    {
        internal ActiveStatusEffect(string kindId)
        {
            KindId = kindId;
        }

        public string KindId { get; }

        public float RemainingSeconds { get; internal set; }

        public float TickSeconds { get; internal set; }

        public int DamagePerTick { get; internal set; }

        public int? SourceId { get; internal set; }

        public string WeaponId { get; internal set; } = string.Empty;

        public string VisualId { get; internal set; } = string.Empty;

        internal float SinceTick { get; set; }
    }
}
