using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// この画面で操作しているプレイヤーの部品。<see cref="GameSignals.LocalPlayer"/> で UI に渡し、UI は照準・ADS・体力などをここから読む。
    /// プレイヤーの作り方によって持たない部品があるので、どれも null 可。
    /// </summary>
    public sealed class LocalPlayerSignal
    {
        public LocalPlayerSignal(
            GameObject player,
            Damageable damageable,
            WeaponController weapon,
            AimDownSights ads,
            PlayerController mover,
            GrenadeThrower grenades,
            AbilityHost abilities,
            PlayerActionReader actions)
        {
            Player = player;
            Damageable = damageable;
            Weapon = weapon;
            Ads = ads;
            Mover = mover;
            Grenades = grenades;
            Abilities = abilities;
            Actions = actions;
        }

        /// <summary>プレイヤーの GameObject。</summary>
        public GameObject Player { get; }

        public Damageable Damageable { get; }

        public WeaponController Weapon { get; }

        /// <summary>覗き込み (ADS)。</summary>
        public AimDownSights Ads { get; }

        public PlayerController Mover { get; }

        public GrenadeThrower Grenades { get; }

        /// <summary>能力の枠。</summary>
        public AbilityHost Abilities { get; }

        /// <summary>今のアクションをまとめたもの。</summary>
        public PlayerActionReader Actions { get; }
    }
}
