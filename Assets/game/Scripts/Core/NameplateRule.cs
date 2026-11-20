using System;

namespace Shooter.Core
{
    // 味方を壁越しでも出すのは、どこにいるか分かる方が連携しやすいため。
    // 敵を見えているときだけ出すのは、壁越しに出すと位置が透けて見えてしまうため。
    public static class NameplateRule
    {
        public static bool ShouldShow(NameplateSpec spec, TeamRelation relation, float distance, bool inSight, bool alive)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!alive || !(distance >= 0f) || float.IsInfinity(distance))
            {
                return false;
            }

            switch (relation)
            {
                case TeamRelation.Friendly:
                    return WithinRange(distance, spec.FriendlyRange);
                case TeamRelation.Enemy:
                    return inSight && WithinRange(distance, spec.EnemyRange);
                case TeamRelation.Neutral:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(relation), relation, "unknown team relation");
            }
        }

        // 距離の比較だけにしないのは、範囲 0 (その相手には出さない) でも重なった距離 0 の相手に出てしまうため。
        private static bool WithinRange(float distance, float range)
        {
            return range > 0f && distance <= range;
        }
    }
}
