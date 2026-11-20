using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 掛けるのに回数と待ち時間を使い、見え方の切り替えと外すのは回数を使わない。持ち主に VisionModeController が無ければ使えない。
    [CreateAssetMenu(fileName = "VisionGoggles", menuName = "Shooter/Abilities/Vision Goggles")]
    public sealed class VisionGogglesDefinition : AbilityDefinition
    {
        public const VisionMode DefaultMode = VisionMode.Night;

        [SerializeField, Tooltip("掛けたときの見え方。None は選べない")]
        private VisionMode _mode = DefaultMode;

        // 1 つ目を _mode のまま残すのは、見え方を 1 つしか持たなかった頃に作ったアセットをそのまま読めるようにするため。
        [SerializeField, Tooltip("掛けたまま押すたびに、この順に切り替える見え方。最後の次に押すと外す。None と重なりは選べない")]
        private VisionMode[] _thenModes = Array.Empty<VisionMode>();

        public VisionMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        // 掛けたときの見え方から順に並べた、押すたびに進む見え方。
        public IReadOnlyList<VisionMode> Modes
        {
            get
            {
                var then = _thenModes ?? Array.Empty<VisionMode>();
                var modes = new VisionMode[then.Length + 1];
                modes[0] = _mode;
                Array.Copy(then, 0, modes, 1, then.Length);
                return modes;
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    throw new ArgumentException("goggles need at least one mode", nameof(value));
                }

                _mode = value[0];
                var then = new VisionMode[value.Count - 1];
                for (var i = 1; i < value.Count; i++)
                {
                    then[i - 1] = value[i];
                }

                _thenModes = then;
            }
        }

        public VisionMode ToMode()
        {
            return ToModes()[0];
        }

        public IReadOnlyList<VisionMode> ToModes()
        {
            var modes = Modes;
            for (var i = 0; i < modes.Count; i++)
            {
                var mode = modes[i];
                if (!Enum.IsDefined(typeof(VisionMode), mode) || mode == VisionMode.None)
                {
                    throw new AbilityDefinitionException($"vision goggles '{Id}' is invalid: mode {i} must be Night or Thermal (was {mode})");
                }

                // 次の見え方は掛けている見え方の位置から探すので、同じ見え方が 2 つあると次が決まらない。
                for (var j = 0; j < i; j++)
                {
                    if (modes[j] == mode)
                    {
                        throw new AbilityDefinitionException($"vision goggles '{Id}' is invalid: mode {mode} is listed twice");
                    }
                }
            }

            return modes;
        }

        public override IAbility CreateAbility()
        {
            return new VisionGogglesAbility(ToModes());
        }
    }
}
