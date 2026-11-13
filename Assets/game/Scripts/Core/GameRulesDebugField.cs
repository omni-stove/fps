namespace Fps.Core
{
    /// <summary>
    /// ルールのデバッグパネル (<see cref="GameRulesDebugDraft"/>) で切り替えられる項目。並びはパネルの並び順。
    /// </summary>
    public enum GameRulesDebugField
    {
        /// <summary><see cref="GameRules.FriendlyFire"/>。</summary>
        FriendlyFire,

        /// <summary><see cref="GameRules.RespawnMode"/>。</summary>
        RespawnMode,

        /// <summary><see cref="GameRules.DeathBody"/>。</summary>
        DeathBody,

        /// <summary><see cref="GameRules.DeathCamera"/>。</summary>
        DeathCamera,

        /// <summary><see cref="GameRules.InfiniteReserve"/>。</summary>
        InfiniteReserve,

        /// <summary><see cref="GameRules.Destruction"/>。</summary>
        Destruction,

        /// <summary><see cref="ActionRules.Downed"/>。</summary>
        Downed,

        /// <summary><see cref="ActionRules.Cover"/>。</summary>
        Cover,

        /// <summary><see cref="ActionRules.BlindFire"/>。</summary>
        BlindFire,

        /// <summary><see cref="ActionRules.ActiveReload"/>。</summary>
        ActiveReload,

        /// <summary><see cref="ActionRules.Dodge"/>。</summary>
        Dodge,

        /// <summary><see cref="ActionRules.Vault"/>。</summary>
        Vault,

        /// <summary><see cref="ActionRules.Grenade"/>。</summary>
        Grenade,

        /// <summary><see cref="GameRules.FallDamage"/>。</summary>
        FallDamage,

        /// <summary><see cref="GameRules.HudDensity"/>。</summary>
        HudDensity,
    }
}
