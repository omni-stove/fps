using UnityEngine;

namespace Shooter.Runtime
{
    // プレイヤーの作り方によって持たない部品があるので、どれも null を許す。
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

        public GameObject Player { get; }

        public Damageable Damageable { get; }

        public WeaponController Weapon { get; }

        public AimDownSights Ads { get; }

        public PlayerController Mover { get; }

        public GrenadeThrower Grenades { get; }

        public AbilityHost Abilities { get; }

        public PlayerActionReader Actions { get; }
    }
}
