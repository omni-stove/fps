using System;

namespace Fps.Core
{
    /// <summary>
    /// 立ち・しゃがみ・伏せのカプセル・目の寸法を出す。低い姿勢ではカプセルを足元に合わせたまま縮め (足元の Y は変えない)、
    /// 目は頭のてっぺんからの距離を保ったまま下げる。Y はすべてプレイヤー本体のローカル座標 (m)。
    /// </summary>
    public sealed class StanceGeometry
    {
        /// <summary>立ちの寸法。</summary>
        public StanceShape Standing { get; }

        /// <summary>しゃがみの寸法。</summary>
        public StanceShape Crouched { get; }

        /// <summary>伏せの寸法。伏せの高さを渡さずに作ったときはしゃがみと同じ。</summary>
        public StanceShape Prone { get; }

        /// <summary>カプセルの半径。</summary>
        public float Radius { get; }

        /// <summary>伏せの高さを渡さずに作る。<see cref="Prone"/> はしゃがみと同じ寸法になる。</summary>
        /// <param name="standingHeight">立ちのカプセルの高さ。正の有限値。</param>
        /// <param name="standingCenterY">立ちのカプセルの中心の Y。有限値。</param>
        /// <param name="standingEyeY">立ちの目の Y。有限値。</param>
        /// <param name="crouchHeight">しゃがみのカプセルの高さ。半径の 2 倍以上、立ちの高さ以下。</param>
        /// <param name="radius">カプセルの半径。0 以上の有限値。</param>
        public StanceGeometry(float standingHeight, float standingCenterY, float standingEyeY, float crouchHeight, float radius)
            : this(standingHeight, standingCenterY, standingEyeY, crouchHeight, crouchHeight, radius)
        {
        }

        /// <param name="standingHeight">立ちのカプセルの高さ。正の有限値。</param>
        /// <param name="standingCenterY">立ちのカプセルの中心の Y。有限値。</param>
        /// <param name="standingEyeY">立ちの目の Y。有限値。</param>
        /// <param name="crouchHeight">しゃがみのカプセルの高さ。半径の 2 倍以上、立ちの高さ以下。</param>
        /// <param name="proneHeight">伏せのカプセルの高さ。半径の 2 倍以上、しゃがみの高さ以下。</param>
        /// <param name="radius">カプセルの半径。0 以上の有限値。</param>
        public StanceGeometry(
            float standingHeight, float standingCenterY, float standingEyeY, float crouchHeight, float proneHeight, float radius)
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

            // 伏せから立つときにしゃがみを経て確かめるので、しゃがみより高い伏せは認めない。
            if (!(proneHeight > 0f && proneHeight <= crouchHeight && proneHeight >= radius * 2f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(proneHeight),
                    proneHeight,
                    $"proneHeight must be positive, at least twice the radius ({radius * 2f}) and not above crouchHeight ({crouchHeight})");
            }

            Radius = radius;
            Standing = new StanceShape(standingHeight, standingCenterY, standingEyeY);
            Crouched = Lowered(Standing, crouchHeight);
            Prone = Lowered(Standing, proneHeight);
        }

        /// <summary>姿勢に応じた寸法。crouched が false なら立ち。</summary>
        public StanceShape Shape(bool crouched)
        {
            return crouched ? Crouched : Standing;
        }

        /// <summary>姿勢に応じた寸法。</summary>
        /// <exception cref="ArgumentOutOfRangeException">stance が定義済みの値でない。</exception>
        public StanceShape Shape(PlayerStance stance)
        {
            switch (stance)
            {
                case PlayerStance.Standing:
                    return Standing;
                case PlayerStance.Crouching:
                    return Crouched;
                case PlayerStance.Prone:
                    return Prone;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stance), stance, "stance is not a defined PlayerStance");
            }
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

        private static StanceShape Lowered(StanceShape standing, float height)
        {
            var drop = standing.Height - height;
            var feetY = standing.BottomY;

            // 目が足元より下へ行かないようにする。ただし元から足元より下に置かれた目はそのまま。
            var eyeY = Math.Max(standing.EyeY - drop, Math.Min(standing.EyeY, feetY));
            return new StanceShape(height, feetY + (height * 0.5f), eyeY);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
