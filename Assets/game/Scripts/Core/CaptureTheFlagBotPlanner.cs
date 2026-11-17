using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class CaptureTheFlagBotPlanner
    {
        public const float DefaultAttackShare = 0.5f;

        // 0.3f のように 2 進で割り切れない割合を掛けると、ちょうどの人数をわずかに超えて 1 人多く切り上がるので、この差までは切り上げない。
        private const double _roundingSlack = 1e-6;

        // 旗を運ぶのと落ちた自陣の旗を取り返すのは、戦っていても先にする。運ばなければ点にならず、放っておくと敵に拾われるので。
        public static BotErrand Plan(
            bool carrying,
            bool attacker,
            Vector3 ownBase,
            FlagState ownFlag,
            Vector3 ownFlagPosition,
            Vector3 enemyFlagPosition)
        {
            if (carrying)
            {
                return new BotErrand(BotErrandKind.CarryFlag, ownBase, -1, true);
            }

            if (ownFlag == FlagState.Dropped)
            {
                return new BotErrand(BotErrandKind.RecoverFlag, ownFlagPosition, -1, true);
            }

            return attacker
                ? new BotErrand(BotErrandKind.TakeFlag, enemyFlagPosition, -1, false)
                : new BotErrand(BotErrandKind.DefendFlag, ownBase, -1, false);
        }

        // 切り上げるのは、attackShare が 0 より大きければ 1 人だけの陣営でもその 1 人に攻めさせるため。
        public static bool IsAttacker(int teamIndex, int teamSize, float attackShare = DefaultAttackShare)
        {
            if (teamSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(teamSize), teamSize, "teamSize must be at least 1");
            }

            if (teamIndex < 0 || teamIndex >= teamSize)
            {
                throw new ArgumentOutOfRangeException(nameof(teamIndex), teamIndex, "teamIndex must be within the team");
            }

            if (!(attackShare >= 0f && attackShare <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(attackShare), attackShare, "attackShare must be between 0 and 1");
            }

            var attackers = (int)Math.Ceiling((teamSize * (double)attackShare) - _roundingSlack);
            return teamIndex < attackers;
        }
    }
}
