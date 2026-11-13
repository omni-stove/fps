using System;

namespace Fps.Core
{
    /// <summary>
    /// 立ちとしゃがみのカプセル・目の寸法を出す。しゃがむとカプセルを足元に合わせたまま縮め (足元の Y は変えない)、
    /// 目は頭のてっぺんからの距離を保ったまま下げる。Y はすべてプレイヤー本体のローカル座標 (m)。
    /// </summary>
    public sealed class StanceGeometry
    {
        /// <summary>立ちの寸法。</summary>
        public StanceShape Standing { get; }

        /// <summary>しゃがみの寸法。</summary>
        public StanceShape Crouched { get; }

        /// <summary>カプセルの半径。</summary>
        public float Radius { get; }

        /// <param name="standingHeight">立ちのカプセルの高さ。正の有限値。</param>
        /// <param name="standingCenterY">立ちのカプセルの中心の Y。有限値。</param>
        /// <param name="standingEyeY">立ちの目の Y。有限値。</param>
        /// <param name="crouchHeight">しゃがみのカプセルの高さ。半径の 2 倍以上、立ちの高さ以下。</param>
        /// <param name="radius">カプセルの半径。0 以上の有限値。</param>
        public StanceGeometry(float standingHeight, float standingCenterY, float standingEyeY, float crouchHeight, float radius)
        {
            if (!(standingHeight > 0f) || float.IsInfinity(standingHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(standingHeight), standingHeight, "standingHeight must be finite and positive");
            }

            if (!IsFinite(standingCenterY))
            {
                throw new ArgumentOutOfRangeException(nameof(standingCenterY), standingCenterY, "standingCenterY must be finite");
            }

            if (!IsFinite(standingEyeY))
            {
                throw new ArgumentOutOfRangeException(nameof(standingEyeY), standingEyeY, "standingEyeY must be finite");
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be finite and not negative");
            }

            // 半径の 2 倍より低いと CharacterController は球として扱い、足元を保てなくなる。
            if (!(crouchHeight > 0f && crouchHeight <= standingHeight && crouchHeight >= radius * 2f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(crouchHeight),
                    crouchHeight,
                    $"crouchHeight must be positive, at least twice the radius ({radius * 2f}) and not above standingHeight ({standingHeight})");
            }

            Radius = radius;
            Standing = new StanceShape(standingHeight, standingCenterY, standingEyeY);

            var drop = standingHeight - crouchHeight;
            var feetY = Standing.BottomY;

            // 目が足元より下へ行かないようにする。ただし元から足元より下に置かれた目はそのまま。
            var crouchedEyeY = Math.Max(standingEyeY - drop, Math.Min(standingEyeY, feetY));
            Crouched = new StanceShape(crouchHeight, feetY + (crouchHeight * 0.5f), crouchedEyeY);
        }

        /// <summary>姿勢に応じた寸法。</summary>
        public StanceShape Shape(bool crouched)
        {
            return crouched ? Crouched : Standing;
        }

        /// <summary>
        /// カプセルの両端の半球の中心の Y。Physics.CheckCapsule などに渡す。高さが半径の 2 倍に満たなければ両方とも中心。
        /// </summary>
        public void SphereCenters(StanceShape shape, out float bottomY, out float topY)
        {
            var half = Math.Max((shape.Height * 0.5f) - Radius, 0f);
            bottomY = shape.CenterY - half;
            topY = shape.CenterY + half;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
