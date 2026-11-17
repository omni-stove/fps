using System;

namespace Shooter.Core
{
    // 味方を壁越しでも出すのは、どこにいるか分かる方が連携しやすいため。
    // 敵を見えているときだけ出すのは、壁越しに出すと位置が透けて見えてしまうため。
    public static class NameplateRule
    {
        public const float FriendlyRange = 60f;

        public const float EnemyRange = 35f;

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
