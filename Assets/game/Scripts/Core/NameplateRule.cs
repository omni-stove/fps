using System;

namespace Fps.Core
{
    /// <summary>
    /// 他のプレイヤーの頭の上にネームプレートを出すかの決まり。
    /// 味方は壁越しでも <see cref="FriendlyRange"/> 以内なら出す (どこにいるか分かる方が連携しやすい)。
    /// 敵は見えていて <see cref="EnemyRange"/> 以内のときだけ出す (壁越しに出すと位置が透けて見えてしまう)。
    /// </summary>
    public static class NameplateRule
    {
        /// <summary>味方のネームプレートを出す最大の距離 (m)。</summary>
        public const float FriendlyRange = 60f;

        /// <summary>敵のネームプレートを出す最大の距離 (m)。</summary>
        public const float EnemyRange = 35f;

        /// <summary>
        /// relation の相手が distance (m) 先にいて、視線が通る (inSight) ときにネームプレートを出すか。
        /// 倒れている相手 (alive が false) と中立には出さない。distance が負か有限でなければ出さない。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">relation が定義済みの値でない。</exception>
        public static bool ShouldShow(TeamRelation relation, float distance, bool inSight, bool alive)
        {
            if (!alive || !(distance >= 0f) || float.IsInfinity(distance))
            {
                return false;
            }

            switch (relation)
            {
                case TeamRelation.Friendly:
                    return distance <= FriendlyRange;
                case TeamRelation.Enemy:
                    return inSight && distance <= EnemyRange;
                case TeamRelation.Neutral:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(relation), relation, "unknown team relation");
            }
        }
    }
}
