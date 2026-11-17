using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class CameraBoom
    {
        // 0 にしないのは、近すぎるとカメラの near clip が体を切るため。
        public const float DefaultMinLength = 0.2f;

        public static Vector3 Offset(ViewMode mode, Vector3 thirdPersonOffset)
        {
            return Offset(mode, thirdPersonOffset, ShoulderSide.Right);
        }

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

        // 線形に寄せないのは、動き始めと止まり際が急にならないよう、割合を smoothstep で緩めるため。
        public static Vector3 TowardEye(Vector3 offset, float eyeBlend)
        {
            var t = float.IsNaN(eyeBlend) ? 0f : Math.Max(0f, Math.Min(1f, eyeBlend));
            var eased = t * t * (3f - (2f * t));
            return offset * (1f - eased);
        }

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
