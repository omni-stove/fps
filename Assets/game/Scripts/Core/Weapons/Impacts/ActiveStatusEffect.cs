namespace Fps.Core
{
    /// <summary>1 人にかかっている状態異常 1 つ。<see cref="StatusEffects"/> が持ち、進める。</summary>
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

        /// <summary>最後にかけた人。いなければ null。</summary>
        public int? SourceId { get; internal set; }

        /// <summary>最後にかけた武器の Id。null にならない。</summary>
        public string WeaponId { get; internal set; } = string.Empty;

        public string VisualId { get; internal set; } = string.Empty;

        internal float SinceTick { get; set; }
    }
}
