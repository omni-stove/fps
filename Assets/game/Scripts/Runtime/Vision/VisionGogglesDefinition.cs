using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 掛けるのに回数と待ち時間を使い、外すのは回数を使わない。持ち主に VisionModeController が無ければ使えない。
    [CreateAssetMenu(fileName = "VisionGoggles", menuName = "Shooter/Abilities/Vision Goggles")]
    public sealed class VisionGogglesDefinition : AbilityDefinition
    {
        public const VisionMode DefaultMode = VisionMode.Night;

        [SerializeField, Tooltip("掛けている間の見え方。None は選べない")]
        private VisionMode _mode = DefaultMode;

        public VisionMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        public VisionMode ToMode()
        {
            if (!Enum.IsDefined(typeof(VisionMode), _mode) || _mode == VisionMode.None)
            {
                throw new AbilityDefinitionException($"vision goggles '{Id}' is invalid: mode must be Night or Thermal (was {_mode})");
            }

            return _mode;
        }

        public override IAbility CreateAbility()
        {
            return new VisionGogglesAbility(ToMode());
        }
    }
}
