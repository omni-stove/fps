using System;

namespace Fps.Core
{
    /// <summary>
    /// ガーディアン (VIP の護衛) の <see cref="ModeSession"/> を組む。Alpha と Bravo の 2 チーム (<see cref="TwoTeams"/>) で、
    /// 試合が始まる (Playing になる) たびに各チームから 1 人をリーダーに選ぶ (<see cref="GuardianRespawn.ChooseLeaders"/>)。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>リーダーが生きている間は、倒れた味方が respawnSeconds 後に戻る (<see cref="IndividualRespawn"/>)。</item>
    /// <item>リーダーが倒れたチームは、それ以降誰も戻らない。残りも全員倒れたらそのチームの負け (<see cref="LastSideStandingWin"/>)。</item>
    /// <item>時間切れでは、リーダーが生きているチームがちょうど 1 つならそのチームの勝ち (<see cref="LeaderStandingWin"/>)、
    /// そうでなければキル数の多いチームの勝ち (<see cref="KillScore"/>・<see cref="TimeLimitWin"/>)。得点の上限は無い。</item>
    /// </list>
    /// </remarks>
    public static class GuardianMode
    {
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい値。</param>
        /// <param name="respawnSeconds">リーダーが生きている間、倒れてから戻るまでの秒数。0 以上の有限値。</param>
        /// <param name="pick">リーダーを選ぶ。チームの人数が渡り、0 以上その数未満の番号 (playerId の昇順での位置) を返す。</param>
        /// <param name="leaders">組んだリスポーンの部品。リーダーと、倒れたかどうかを引く。</param>
        public static ModeSession CreateSession(float timeLimitSeconds, float respawnSeconds, Func<int, int> pick, out GuardianRespawn leaders)
        {
            if (pick == null)
            {
                throw new ArgumentNullException(nameof(pick));
            }

            var respawn = new GuardianRespawn(new IndividualRespawn(respawnSeconds));
            var session = new ModeSession(
                new TwoTeams(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                respawn,
                new IWinCondition[] { new LastSideStandingWin(), new LeaderStandingWin(respawn), new TimeLimitWin() });

            // Start は参加者を入れた後に呼ぶ約束なので、Playing になった時点で各チームの顔ぶれが揃っている。
            session.StateChanged += state =>
            {
                if (state == GameModeState.Playing)
                {
                    respawn.ChooseLeaders(session, pick);
                }
            };

            leaders = respawn;
            return session;
        }

        /// <summary>random でリーダーを選ぶ <see cref="CreateSession(float, float, Func{int, int}, out GuardianRespawn)"/>。</summary>
        public static ModeSession CreateSession(float timeLimitSeconds, float respawnSeconds, Random random, out GuardianRespawn leaders)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return CreateSession(timeLimitSeconds, respawnSeconds, count => random.Next(count), out leaders);
        }
    }
}
