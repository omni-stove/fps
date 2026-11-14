using System;

namespace Fps.Core
{
    public static class EnemyGait
    {
        // 敵ごとの歩きの速さはシーンが決めるので、プレイヤーの既定 (歩き 5・ダッシュ 8・しゃがみ 2.5 m/s) を比にして掛ける。
        public const float SprintRatio = 1.6f;
        public const float CrouchRatio = 0.5f;

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

        public static float Speed(float walkSpeed, bool sprinting, bool crouching)
        {
            if (!(walkSpeed >= 0f) || float.IsInfinity(walkSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), walkSpeed, "walkSpeed must be a non-negative finite number");
            }

            if (crouching)
            {
                return walkSpeed * CrouchRatio;
            }

            return sprinting ? walkSpeed * SprintRatio : walkSpeed;
        }
    }
}
