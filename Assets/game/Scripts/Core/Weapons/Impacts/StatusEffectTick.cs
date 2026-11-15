namespace Fps.Core
{
    /// <summary><see cref="StatusEffects.Tick"/> で状態異常が与えるダメージ 1 回。かけた人と武器に帰属させる。</summary>
    public readonly struct StatusEffectTick
    {
        public StatusEffectTick(string kindId, int damage, int? sourceId, string weaponId)
        {
            KindId = kindId ?? string.Empty;
            Damage = damage;
            SourceId = sourceId;
            WeaponId = weaponId ?? string.Empty;
        }

        public string KindId { get; }

        public int Damage { get; }

        public int? SourceId { get; }

        public string WeaponId { get; }
    }
}
