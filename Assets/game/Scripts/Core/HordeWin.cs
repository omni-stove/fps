using System;

namespace Fps.Core
{
    /// <summary>
    /// ホードの勝ち負け。波の予定 (<see cref="WaveSchedule"/>) の最後の波を全滅させたら守る側の勝ち、
    /// 守る側が脱落したら (<see cref="ModeSession.IsSideEliminated"/>) 攻める側の勝ち。得点の上限は無い。
    /// </summary>
    /// <remarks>
    /// 最後の敵を倒したのと同じ間に最後の 1 人が倒れたときは、波を越えたことを先に見て守る側の勝ちにする。
    /// 時間切れでは決めない (ホードは時間で終わらない。制限時間を付けたときは引き分けになる)。
    /// </remarks>
    public sealed class HordeWin : IWinCondition
    {
        private readonly WaveSchedule _schedule;

        /// <param name="schedule">波の予定。終わったかどうかだけを読む。</param>
        /// <param name="defenders">守る側 (参加者) の陣営。</param>
        /// <param name="attackers">攻める側 (波の敵) の陣営。defenders と違うこと。</param>
        public HordeWin(WaveSchedule schedule, Side defenders, Side attackers)
        {
            if (defenders == attackers)
            {
                throw new ArgumentException($"defenders and attackers must differ, but both are {defenders}", nameof(attackers));
            }

            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            Defenders = defenders;
            Attackers = attackers;
        }

        public Side Defenders { get; }

        public Side Attackers { get; }

        public double? ScoreLimit => null;

        public ModeOutcome CheckStanding(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (_schedule.Phase == WavePhase.Completed)
            {
                return ModeOutcome.Win(Defenders);
            }

            return session.IsSideEliminated(Defenders) ? ModeOutcome.Win(Attackers) : ModeOutcome.Undecided;
        }

        public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
    }
}
