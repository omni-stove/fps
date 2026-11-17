using System;

namespace Shooter.Core
{
    // 置いた物を起爆できる能力 (C4) は、置くのと起爆するのを同じボタンで受ける。置く操作は押している間に狙って離すので、狙う前に離した押し方を起爆にする。
    public static class DeployReuse
    {
        public static DeployReuseStep Decide(bool held, float heldSeconds, float tapSeconds)
        {
            if (!(heldSeconds >= 0f) || float.IsInfinity(heldSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(heldSeconds), heldSeconds, "heldSeconds must not be negative and must be finite");
            }

            if (!(tapSeconds >= 0f) || float.IsInfinity(tapSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tapSeconds), tapSeconds, "tapSeconds must not be negative and must be finite");
            }

            if (heldSeconds >= tapSeconds)
            {
                return DeployReuseStep.Aim;
            }

            return held ? DeployReuseStep.Wait : DeployReuseStep.Detonate;
        }
    }
}
