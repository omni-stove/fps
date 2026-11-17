using System;

namespace Fps.Core
{
    public static class EnemyGait
    {
        public static bool IsAiming(EnemyState state, EnemyCoverPhase coverPhase)
        {
            return state == EnemyState.Attack || (state == EnemyState.TakeCover && coverPhase == EnemyCoverPhase.Peeking);
        }

        public static bool IsSprinting(EnemyState state, EnemyCoverPhase coverPhase, bool crouching, bool firing)
        {
            // プレイヤーも撃つとダッシュが解ける。
            if (crouching || firing)
            {
                return false;
            }

            return state == EnemyState.Chase || (state == EnemyState.TakeCover && coverPhase == EnemyCoverPhase.Moving);
        }

        /// <param name="walkSpeed">歩きの速さ (m/s)。0 以上の有限値。</param>
        /// <param name="sprinting">走っているか。しゃがんでいれば無視する。</param>
        /// <param name="crouching">しゃがんで動いているか。</param>
        /// <param name="sprintRatio">走るときに歩きの速さへ掛ける倍率 (<see cref="EnemySpec.SprintRatio"/>)。0 より大きい有限値。</param>
        /// <param name="crouchRatio">しゃがんで動くときに歩きの速さへ掛ける倍率 (<see cref="EnemySpec.CrouchRatio"/>)。0 より大きい有限値。</param>
        public static float Speed(float walkSpeed, bool sprinting, bool crouching, float sprintRatio, float crouchRatio)
        {
            if (!(walkSpeed >= 0f) || float.IsInfinity(walkSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), walkSpeed, "walkSpeed must be a non-negative finite number");
            }

            RequirePositive(sprintRatio, nameof(sprintRatio));
            RequirePositive(crouchRatio, nameof(crouchRatio));
            if (crouching)
            {
                return walkSpeed * crouchRatio;
            }

            return sprinting ? walkSpeed * sprintRatio : walkSpeed;
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
