using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class VisionGogglesAbility : IAbility, IAbilityFollowUp
    {
        private readonly VisionMode _mode;
        private bool _done;

        public VisionGogglesAbility(VisionMode mode)
        {
            if (!Enum.IsDefined(typeof(VisionMode), mode) || mode == VisionMode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode must be Night or Thermal");
            }

            _mode = mode;
        }

        public VisionMode Mode => _mode;

        public static VisionModeController ControllerOf(AbilityContext context)
        {
            var owner = context != null ? context.Owner : null;
            return owner != null ? owner.GetComponent<VisionModeController>() : null;
        }

        public bool CanUse(AbilityContext context)
        {
            return ControllerOf(context) != null;
        }

        // 外すのは掛けるのと違って待たせないので、効果が出るのを待たずにここで外し、使った 1 回を返す。
        public void Begin(AbilityContext context)
        {
            _done = false;
            var controller = ControllerOf(context);
            if (controller == null || controller.Goggles != _mode)
            {
                return;
            }

            controller.SetGoggles(VisionMode.None);
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
                controller.SetGoggles(_mode);
            }

            return false;
        }

        public void End(AbilityContext context, bool interrupted)
        {
            _done = false;
        }

        // 回数を使い切っている・待ち時間の間でも外せるよう、枠に断られたときにここで外す。
        public bool TryFollowUp(AbilityContext context)
        {
            var controller = ControllerOf(context);
            if (controller == null || controller.Goggles != _mode)
            {
                return false;
            }

            controller.SetGoggles(VisionMode.None);
            return true;
        }

        // 外すことは HUD の枠に案内を出すほどの操作ではないので、案内は出さない。
        public bool TryGetFollowUp(AbilityContext context, out AbilityFollowUp followUp)
        {
            followUp = default;
            return false;
        }
    }
}
