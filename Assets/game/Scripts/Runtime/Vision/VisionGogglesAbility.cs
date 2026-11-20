using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 外しているときに押すと 1 つ目の見え方で掛け、掛けているときに押すたびに次の見え方へ切り替え、最後の次で外す。
    public sealed class VisionGogglesAbility : IAbility, IAbilityFollowUp
    {
        private readonly VisionMode[] _modes;
        private bool _done;

        public VisionGogglesAbility(VisionMode mode)
            : this(new[] { mode })
        {
        }

        public VisionGogglesAbility(IReadOnlyList<VisionMode> modes)
        {
            if (modes == null || modes.Count == 0)
            {
                throw new ArgumentException("goggles need at least one mode", nameof(modes));
            }

            _modes = new VisionMode[modes.Count];
            for (var i = 0; i < modes.Count; i++)
            {
                var mode = modes[i];
                if (!Enum.IsDefined(typeof(VisionMode), mode) || mode == VisionMode.None)
                {
                    throw new ArgumentOutOfRangeException(nameof(modes), mode, "mode must be Night or Thermal");
                }

                if (Array.IndexOf(_modes, mode, 0, i) >= 0)
                {
                    throw new ArgumentException($"mode {mode} is listed twice", nameof(modes));
                }

                _modes[i] = mode;
            }
        }

        public VisionMode Mode => _modes[0];

        public IReadOnlyList<VisionMode> Modes => _modes;

        public static VisionModeController ControllerOf(AbilityContext context)
        {
            var owner = context != null ? context.Owner : null;
            return owner != null ? owner.GetComponent<VisionModeController>() : null;
        }

        // 掛けているのがこのゴーグルの見え方でなければ null (押すと掛ける)。最後の見え方なら None (押すと外す)。
        public VisionMode? NextAfter(VisionMode current)
        {
            var index = Array.IndexOf(_modes, current);
            if (index < 0)
            {
                return null;
            }

            return index + 1 < _modes.Length ? _modes[index + 1] : VisionMode.None;
        }

        public bool CanUse(AbilityContext context)
        {
            return ControllerOf(context) != null;
        }

        // 切り替えと外すのは掛けるのと違って待たせないので、効果が出るのを待たずにここで変え、使った 1 回を返す。
        public void Begin(AbilityContext context)
        {
            _done = false;
            var controller = ControllerOf(context);
            if (controller == null)
            {
                return;
            }

            var next = NextAfter(controller.Goggles);
            if (!next.HasValue)
            {
                return;
            }

            controller.SetGoggles(next.Value);
            context.Slot.Refund();
            _done = true;
        }

        // 効果が出るまでの秒数 (掛ける動き) を待ってから掛けるので、Begin ではなく効果が出た Tick で掛ける。
        public bool Tick(AbilityContext context, float deltaSeconds)
        {
            if (_done)
            {
                return false;
            }

            if (!context.IsEffective)
            {
                return true;
            }

            _done = true;
            var controller = ControllerOf(context);
            if (controller != null)
            {
                controller.SetGoggles(_modes[0]);
            }

            return false;
        }

        public void End(AbilityContext context, bool interrupted)
        {
            _done = false;
        }

        // 回数を使い切っている・待ち時間の間でも切り替えたり外したりできるよう、枠に断られたときにここで進める。
        public bool TryFollowUp(AbilityContext context)
        {
            var controller = ControllerOf(context);
            if (controller == null)
            {
                return false;
            }

            var next = NextAfter(controller.Goggles);
            if (!next.HasValue)
            {
                return false;
            }

            controller.SetGoggles(next.Value);
            return true;
        }

        // 切り替えや外すことは HUD の枠に案内を出すほどの操作ではないので、案内は出さない。
        public bool TryGetFollowUp(AbilityContext context, out AbilityFollowUp followUp)
        {
            followUp = default;
            return false;
        }
    }
}
