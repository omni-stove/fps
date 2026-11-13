using System;

namespace Fps.Core
{
    /// <summary>
    /// アクティブリロードの判定。リロードの進み (経過時間 / リロード時間) が判定の幅に入っていれば成功にする。
    /// オフライン (<see cref="Weapon.TryActiveReload"/>) とオンラインで同じ判定を使う。
    /// </summary>
    public static class ActiveReloadJudge
    {
        /// <summary>
        /// progress が windowStart 以上、windowStart + windowLength 以下なら <see cref="ActiveReloadResult.Success"/>、
        /// それ以外は <see cref="ActiveReloadResult.Fail"/>。境界は両端とも成功に含む。
        /// </summary>
        /// <param name="progress">リロードの進み。NaN 以外。0〜1 の外なら失敗。</param>
        /// <param name="windowStart">判定の幅の始まり。0 以上 1 未満 (<see cref="WeaponSpec.ActiveReloadWindowStart"/>)。</param>
        /// <param name="windowLength">判定の幅の長さ。0 より大きく、windowStart との和が 1 以下 (<see cref="WeaponSpec.ActiveReloadWindowLength"/>)。</param>
        /// <exception cref="ArgumentOutOfRangeException">progress が NaN か、判定の幅が上の範囲の外。</exception>
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
