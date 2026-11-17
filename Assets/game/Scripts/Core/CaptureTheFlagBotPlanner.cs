using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// 旗取り (<see cref="CaptureTheFlag"/>) の bot が戦いのほかにする用事 (<see cref="BotErrand"/>) を決める。状態は持たず、同じ入力なら同じ用事を返す。
    /// </summary>
    /// <remarks>
    /// 上から順に、最初に当てはまったものを返す。
    /// <list type="number">
    /// <item>敵の旗を運んでいれば、自陣の台へ運ぶ (<see cref="BotErrandKind.CarryFlag"/>。戦っていても優先する。運ばなければ点にならないので)。</item>
    /// <item>自陣の旗が落ちていれば、落ちている所へ取り返しに行く (<see cref="BotErrandKind.RecoverFlag"/>。戦っていても優先する。放っておくと敵に拾われるので)。</item>
    /// <item>攻めの役なら、敵の旗のある所 (台・落ちた所・運ぶ人の足元) へ行く (<see cref="BotErrandKind.TakeFlag"/>)。</item>
    /// <item>守りの役なら、自陣の台へ行く (<see cref="BotErrandKind.DefendFlag"/>)。</item>
    /// </list>
    /// 攻めと守りでは、敵に出会えばいつもの戦いを優先する。役は <see cref="IsAttacker"/> で陣営の中の並びから割り振る。
    /// </remarks>
    public static class CaptureTheFlagBotPlanner
    {
        /// <summary>陣営のうち攻めの役にする割合の既定値。</summary>
        public const float DefaultAttackShare = 0.5f;

        // 0.3f のように 2 進で割り切れない割合を掛けると、ちょうどの人数をわずかに超えて 1 人多く切り上がるので、この差までは切り上げない。
        private const double _roundingSlack = 1e-6;

        /// <param name="carrying">bot が敵の旗を運んでいれば true。</param>
        /// <param name="attacker">bot が攻めの役なら true、守りの役なら false (<see cref="IsAttacker"/>)。</param>
        /// <param name="ownBase">自陣の旗の台の足元。</param>
        /// <param name="ownFlag">自陣の旗の状態。</param>
        /// <param name="ownFlagPosition">自陣の旗の今の足元 (台・落ちた所・運ぶ人の足元)。</param>
        /// <param name="enemyFlagPosition">敵の旗の今の足元 (台・落ちた所・運ぶ人の足元)。</param>
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

        /// <summary>
        /// 陣営の中で teamIndex 番目 (0 から) の bot が攻めの役なら true。陣営の人数に attackShare を掛けて切り上げた人数を、前から順に攻めにする。
        /// attackShare が 0 より大きければ、1 人だけの陣営でもその 1 人は攻める。
        /// </summary>
        /// <param name="teamIndex">陣営の中の並び。0 以上で teamSize 未満。</param>
        /// <param name="teamSize">陣営の人数。1 以上。</param>
        /// <param name="attackShare">攻めの役にする割合。0 以上 1 以下。</param>
        /// <exception cref="ArgumentOutOfRangeException">どれかの値が範囲の外。</exception>
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
