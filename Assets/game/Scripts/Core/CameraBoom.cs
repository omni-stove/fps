using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 目からカメラまでの腕 (ブーム) の計算。三人称では目から離した位置にカメラを置き、
    /// 間に壁があれば壁の手前まで縮めてカメラが壁にめり込まないようにする。
    /// </summary>
    public static class CameraBoom
    {
        /// <summary>壁に寄ったときも目にこれ以上は近づけない距離 (m)。0 だと近すぎてカメラの near clip が体を切る。</summary>
        public const float DefaultMinLength = 0.2f;

        /// <summary>
        /// 目から見たカメラの位置 (目のローカル座標)。一人称は目そのもの (0)、三人称は thirdPersonOffset。
        /// <see cref="ShoulderSide.Right"/> を渡した <see cref="Offset(ViewMode, Vector3, ShoulderSide)"/> と同じ。
        /// </summary>
        public static Vector3 Offset(ViewMode mode, Vector3 thirdPersonOffset)
        {
            return Offset(mode, thirdPersonOffset, ShoulderSide.Right);
        }

        /// <summary>
        /// 目から見たカメラの位置 (目のローカル座標)。一人称は目そのもの (0)、三人称は thirdPersonOffset で、
        /// side が <see cref="ShoulderSide.Left"/> なら x (左右) の符号を反転する。
        /// </summary>
        public static Vector3 Offset(ViewMode mode, Vector3 thirdPersonOffset, ShoulderSide side)
        {
            // 一人称でも side を検査するのは、不正な値が視点を切り替えた時点で初めて見つかるのを避けるため。
            if (side != ShoulderSide.Right && side != ShoulderSide.Left)
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "unknown shoulder side");
            }

            switch (mode)
            {
                case ViewMode.FirstPerson:
                    return Vector3.Zero;
                case ViewMode.ThirdPerson:
                    return side == ShoulderSide.Left
                        ? new Vector3(-thirdPersonOffset.X, thirdPersonOffset.Y, thirdPersonOffset.Z)
                        : thirdPersonOffset;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown view mode");
            }
        }

        /// <summary>
        /// 腕の長さ。間を遮るものが無ければ desired のまま、あればその手前 (blockedAt) まで縮める。
        /// ただし minLength より短くはしない (desired がそれより短ければ desired)。
        /// </summary>
        /// <param name="desired">遮るものが無いときの長さ (m)。0 以上の有限値。</param>
        /// <param name="blockedAt">目からカメラへ球を飛ばして当たった距離 (m)。当たらなければ null。</param>
        /// <param name="minLength">縮めるときの下限 (m)。0 以上の有限値。</param>
        public static float Length(float desired, float? blockedAt, float minLength = DefaultMinLength)
        {
            if (!IsFiniteNonNegative(desired))
            {
                throw new ArgumentOutOfRangeException(nameof(desired), desired, "desired must be finite and not negative");
            }

            if (!IsFiniteNonNegative(minLength))
            {
                throw new ArgumentOutOfRangeException(nameof(minLength), minLength, "minLength must be finite and not negative");
            }

            if (!blockedAt.HasValue || float.IsNaN(blockedAt.Value) || blockedAt.Value >= desired)
            {
                return desired;
            }

            return Math.Min(desired, Math.Max(minLength, blockedAt.Value));
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
