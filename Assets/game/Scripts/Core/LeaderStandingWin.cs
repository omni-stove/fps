using System;

namespace Fps.Core
{
    /// <summary>
    /// 時間切れで、リーダーが倒れていない陣営がちょうど 1 つならその陣営の勝ち (<see cref="GuardianRespawn"/>)。
    /// 両方とも生きているか両方とも倒れているときは決めず、後ろの条件 (<see cref="TimeLimitWin"/> など) に任せる。試合中は何も決めない。
    /// </summary>
    public sealed class LeaderStandingWin : IWinCondition
    {
        private readonly GuardianRespawn _respawn;

        public LeaderStandingWin(GuardianRespawn respawn)
        {
            _respawn = respawn ?? throw new ArgumentNullException(nameof(respawn));
        }

        public double? ScoreLimit => null;

        public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

        public ModeOutcome CheckTimeUp(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var standingCount = 0;
            Side standing = default;
            foreach (var side in _respawn.LeaderSides)
            {
                if (!_respawn.HasLeaderFallen(side))
                {
                    standingCount++;
                    standing = side;
                }
            }

            // リーダーが 1 陣営にしかいない (相手が空だった) ときに勝ちと決めないよう、両陣営にリーダーがいたときだけ比べる。
            return standingCount == 1 && _respawn.LeaderSides.Count > 1 ? ModeOutcome.Win(standing) : ModeOutcome.Undecided;
        }
    }
}
