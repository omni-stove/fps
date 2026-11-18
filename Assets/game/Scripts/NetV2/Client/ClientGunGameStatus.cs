using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いたスナップショットから、オフラインの GunGameRunner が GameSignals.ModeStatus に出すのと同じ形の ModeStatus を作る。
    // 出すところまでしないのは、GameSignals が Runtime にあり、このアセンブリ (Core と Protocol だけを見る) から触れないため。
    // 梯子をサーバーの設定から作らずに受け取るのは、HUD が武器の表示名の鍵を引くのに、名前の鍵を持つ側 (GunGameLadderDefinition) の梯子が要るため
    public sealed class ClientGunGameStatus
    {
        private readonly GunGame _gunGame;
        private readonly ModeSession _ranking;
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();

        public ClientGunGameStatus(GunGameLadder ladder)
        {
            _gunGame = new GunGame(ladder ?? throw new ArgumentNullException(nameof(ladder)));

            // 時間切れの順位を Core の GunGame.CheckTimeUp で引くためだけの ModeSession。Tick しないので制限時間は使わない
            _ranking = new ModeSession(
                new FreeForAll(),
                float.MaxValue,
                Array.Empty<IScoreRule>(),
                null,
                new IWinCondition[] { _gunGame });
        }

        public GunGame GunGame => _gunGame;

        // サーバーの段の数がこの梯子より多い段を送ってきたら true。例外にせず梯子の最後の段に寄せて見せ、知らせるかは使う側が決める
        public bool LadderMismatch { get; private set; }

        // Gun Game の試合でなければ null
        public ModeStatus Update(SnapshotMessage snapshot, int localPlayerId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.GunGame)
            {
                return null;
            }

            var tierCount = _gunGame.Ladder.TierCount;
            var players = snapshot.Players;
            var scores = new List<SideScore>(players.Count);
            var hasLocal = false;
            _gunGame.Reset();
            _present.Clear();
            LadderMismatch = false;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                var playerId = row.PlayerId;
                if (playerId < 0 || !_present.Add(playerId))
                {
                    continue;
                }

                var side = new Side(playerId);
                var tier = row.GunGameTier;
                if (tier > tierCount)
                {
                    LadderMismatch = true;
                    tier = tierCount;
                }

                _gunGame.SetTier(playerId, Math.Max(0, tier), side);
                if (!_ranking.HasPlayer(playerId))
                {
                    _ranking.AddPlayer(playerId, side);
                }

                scores.Add(new SideScore(side, row.Kills));
                hasLocal |= playerId == localPlayerId;
            }

            RemoveGone();

            var ended = match.State == GameModeState.Ended;
            Side? winner = null;
            if (ended)
            {
                winner = _gunGame.FinisherSide ?? _gunGame.CheckTimeUp(_ranking).Winner;
            }

            // 出直しまでの秒はスナップショットに載らないので、オフラインと違い出さない
            return new ModeStatus(
                match.RemainingSeconds,
                scores,
                hasLocal ? new Side(localPlayerId) : (Side?)null,
                null,
                ended,
                winner,
                new GunGameStatus(_gunGame, hasLocal ? localPlayerId : (int?)null));
        }

        private void RemoveGone()
        {
            _gone.Clear();
            foreach (var playerId in _ranking.GetPlayerIds())
            {
                if (!_present.Contains(playerId))
                {
                    _gone.Add(playerId);
                }
            }

            foreach (var playerId in _gone)
            {
                _ranking.RemovePlayer(playerId);
            }
        }
    }
}
