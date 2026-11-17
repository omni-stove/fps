using System;

namespace Shooter.Core
{
    // オフラインの Weapon.TryActiveReload とオンラインで判定がずれないよう、どちらもここを通す。
    public static class ActiveReloadJudge
    {
        public static ActiveReloadResult Judge(float progress, float windowStart, float windowLength)
        {
            if (float.IsNaN(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must not be NaN");
            }

            if (!(windowStart >= 0f && windowStart < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(windowStart), windowStart, "windowStart must be at least 0 and less than 1");
            }

            if (!(windowLength > 0f && windowStart + windowLength <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(windowLength), windowLength, "windowLength must be positive and end the window at or before 1");
            }

            // 終わりを WeaponSpec の検査と同じ float の和で求めるのは、検査を通った幅の端が判定でずれないようにするため。
            var windowEnd = windowStart + windowLength;
            return progress >= windowStart && progress <= windowEnd ? ActiveReloadResult.Success : ActiveReloadResult.Fail;
        }
    }
}
