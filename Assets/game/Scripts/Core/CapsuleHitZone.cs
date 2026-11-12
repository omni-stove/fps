using System;

namespace Fps.Core
{
    /// <summary>
    /// 縦長のカプセル 1 つで表したプレイヤーの、どの高さに当たったかで部位を決める。
    /// 下から <see cref="LimbFraction"/> 未満は手足 (脚)、<see cref="HeadFraction"/> 以上は頭、その間は胴体。
    /// </summary>
    public static class CapsuleHitZone
    {
        /// <summary>この割合の高さ以上は頭。</summary>
        public const float HeadFraction = 0.85f;

        /// <summary>この割合の高さ未満は手足。</summary>
        public const float LimbFraction = 0.4f;

        /// <summary>命中点の高さから部位を返す。カプセルの範囲外の高さは近い端の部位にする。</summary>
        /// <param name="hitHeight">命中点の高さ。有限値。</param>
        /// <param name="bottom">カプセルの底の高さ。有限値。</param>
        /// <param name="height">カプセルの高さ。0 より大きい有限値。</param>
        public static HitZone Classify(float hitHeight, float bottom, float height)
        {
            if (float.IsNaN(hitHeight) || float.IsInfinity(hitHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(hitHeight), hitHeight, "hitHeight must be finite");
            }

            if (float.IsNaN(bottom) || float.IsInfinity(bottom))
            {
                throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "bottom must be finite");
            }

            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive and finite");
            }

            var fraction = ((double)hitHeight - bottom) / height;
            if (fraction >= HeadFraction)
            {
                return HitZone.Head;
            }

            return fraction < LimbFraction ? HitZone.Limb : HitZone.Body;
        }
    }
}
