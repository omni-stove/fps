using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class BootstrapWorld
    {
        public GameObject Floor { get; }
        public GameObject Player { get; }
        public CharacterController Controller { get; }
        public Camera Camera { get; }

        public ViewRig Rig { get; }

        public FirstPersonLook Look { get; }
        public PlayerController Mover { get; }
        public Light Light { get; }
        public WeaponController Weapon { get; }

        public AimDownSights Ads { get; }

        public GameObject TargetRoot { get; }

        public IReadOnlyList<TargetDummy> Targets { get; }

        public Damageable PlayerDamageable { get; }

        public HudModel HudModel { get; }

        public GameRules Rules { get; }

        public HealthRegen Regen { get; }

        public PlayerActionReader ActionReader { get; }

        public GrenadeThrower Grenades { get; }

        public PlayerDeathCamera DeathCamera { get; }

        public AbilityHost Abilities { get; }

        public BootstrapWorld(
            GameObject floor,
            GameObject player,
            CharacterController controller,
            Camera camera,
            FirstPersonLook look,
            PlayerController mover,
            Light light,
            WeaponController weapon,
            GameObject targetRoot,
            IReadOnlyList<TargetDummy> targets,
            Damageable playerDamageable,
            HudModel hudModel,
            ViewRig rig = null,
            AimDownSights ads = null,
            GameRules rules = null,
            HealthRegen regen = null,
            PlayerActionReader actionReader = null,
            GrenadeThrower grenades = null,
            PlayerDeathCamera deathCamera = null,
            AbilityHost abilities = null)
        {
            Abilities = abilities;
            DeathCamera = deathCamera;
            ActionReader = actionReader;
            Grenades = grenades;
            Rig = rig;
            Ads = ads;
            Rules = rules ?? GameRules.Default;
            Regen = regen;
            HudModel = hudModel ?? new HudModel();
            Floor = floor;
            Player = player;
            Controller = controller;
            Camera = camera;
            Look = look;
            Mover = mover;
            Light = light;
            Weapon = weapon;
            TargetRoot = targetRoot;
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
            PlayerDamageable = playerDamageable;
        }
    }
}
