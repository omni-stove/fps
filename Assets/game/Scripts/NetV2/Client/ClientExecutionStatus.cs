using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いたスナップショットから、オフラインの ExecutionRunner と同じ形の ModeStatus (Detail は ExecutionStatus) を作り、UI が ExecutionStatus に登録した書式役をそのまま使えるようにする。
    // サーバー (ExecutionRules) はラウンドの勝ち数を AlphaPoints・BravoPoints に、ラウンドの番号・勝ちに要る数・前のラウンドの勝者を RoundResult の欄に載せる。
    // 残りの人数・ダウン・とどめの進み具合は各行の体力と DownedSync から数え、自分が刺している相手を InteractTargetId にする
    public sealed class ClientExecutionStatus
    {
        private readonly int _localPlayerId;
        private readonly List<Row> _rows = new List<Row>();
        private View _view;

        public ClientExecutionStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Execution の試合のスナップショットが届いていなければ null。変わるたびに別のものになる
        public ModeStatus Status { get; private set; }

        public ExecutionStatus Execution => Status?.Detail as ExecutionStatus;

        // Status を作り直したら true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.Execution)
            {
                return Clear();
            }

            _rows.Clear();
            Side? ownSide = null;
            int? executeTargetId = null;
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var player = ref players[i];
                var downed = player.Downed;
                var executed = downed.IsDowned && downed.HasExecutor;
                _rows.Add(new Row(
                    player.PlayerId,
                    Side.FromTeam(player.Team),
                    player.Health > 0 || downed.IsDowned,
                    downed.IsDowned,
                    downed.IsDowned ? downed.BleedoutSeconds : 0,
                    downed.IsDowned ? downed.RevivePercent : 0,
                    executed ? downed.ExecutorId : (int?)null,
                    executed ? downed.ExecutePercent : 0));
                if (player.PlayerId == _localPlayerId)
                {
                    ownSide = Side.FromTeam(player.Team);
                }

                // サーバーの技は振る人ごとに 1 つなので、自分が刺している行は多くても 1 つ。崩れた値が届いても最初の行に決める
                if (executed && downed.ExecutorId == _localPlayerId && !executeTargetId.HasValue)
                {
                    executeTargetId = player.PlayerId;
                }
            }

            var phase = match.HasRound ? match.Round : RoundPhase.Waiting;
            var remaining = Math.Max(0, match.RemainingSeconds);
            var ended = match.State == GameModeState.Ended;
            var view = new View(
                phase,
                Math.Max(0, match.RoundNumber),
                Math.Max(0, match.RoundsToWin),
                phase == RoundPhase.Preparing ? remaining : 0,
                phase == RoundPhase.Live ? remaining : 0,
                match.HasLastRound,
                match.HasLastRound && match.HasLastRoundWinner ? Side.FromTeam(match.LastRoundWinner) : (Side?)null,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null,
                Math.Max(0, match.AlphaPoints),
                Math.Max(0, match.BravoPoints),
                _rows.ToArray());

            var localPlayerId = ownSide.HasValue ? _localPlayerId : (int?)null;
            var interactTargetId = localPlayerId.HasValue ? executeTargetId : null;
            if (Status != null && _view != null && _view.SameAs(view) && Status.OwnSide == ownSide && Status.IsEnded == ended
                && Status.RemainingSeconds == remaining && Execution?.InteractTargetId == interactTargetId)
            {
                return false;
            }

            _view = view;

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            Status = new ModeStatus(
                remaining,
                new[] { new SideScore(Side.Alpha, view.AlphaWins), new SideScore(Side.Bravo, view.BravoWins) },
                ownSide,
                null,
                ended,
                view.Winner,
                ExecutionStatus.FromView(view, localPlayerId, interactTargetId));
            return true;
        }

        // 試合を離れたときや、Execution でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            if (Status == null)
            {
                return false;
            }

            Status = null;
            _view = null;
            _rows.Clear();
            return true;
        }

        private readonly struct Row
        {
            public Row(
                int playerId, Side side, bool isAlive, bool isDowned, int bleedoutSeconds, int revivePercent, int? executorId, int executePercent)
            {
                PlayerId = playerId;
                Side = side;
                IsAlive = isAlive;
                IsDowned = isDowned;
                BleedoutSeconds = bleedoutSeconds;
                RevivePercent = revivePercent;
                ExecutorId = executorId;
                ExecutePercent = executePercent;
            }

            public int PlayerId { get; }

            public Side Side { get; }

            public bool IsAlive { get; }

            public bool IsDowned { get; }

            public int BleedoutSeconds { get; }

            public int RevivePercent { get; }

            public int? ExecutorId { get; }

            public int ExecutePercent { get; }

            public bool SameAs(in Row other)
            {
                return PlayerId == other.PlayerId && Side == other.Side && IsAlive == other.IsAlive && IsDowned == other.IsDowned
                    && BleedoutSeconds == other.BleedoutSeconds && RevivePercent == other.RevivePercent
                    && ExecutorId == other.ExecutorId && ExecutePercent == other.ExecutePercent;
            }
        }

        private sealed class View : IExecutionView
        {
            private readonly Row[] _rows;

            public View(
                RoundPhase phase,
                int roundNumber,
                int winsToWin,
                float preparationRemaining,
                float roundRemaining,
                bool hasFinishedRound,
                Side? lastRoundWinner,
                Side? winner,
                int alphaWins,
                int bravoWins,
                Row[] rows)
            {
                Phase = phase;
                RoundNumber = roundNumber;
                WinsToWin = winsToWin;
                PreparationRemaining = preparationRemaining;
                RoundRemaining = roundRemaining;
                HasFinishedRound = hasFinishedRound;
                LastRoundWinner = lastRoundWinner;
                Winner = winner;
                AlphaWins = alphaWins;
                BravoWins = bravoWins;
                _rows = rows;
            }

            public RoundPhase Phase { get; }

            public int RoundNumber { get; }

            public int WinsToWin { get; }

            public float PreparationRemaining { get; }

            public float RoundRemaining { get; }

            public bool HasFinishedRound { get; }

            public Side? LastRoundWinner { get; }

            public Side? Winner { get; }

            public int AlphaWins { get; }

            public int BravoWins { get; }

            public int GetRoundWins(Side side)
            {
                if (side == Side.Alpha)
                {
                    return AlphaWins;
                }

                if (side == Side.Bravo)
                {
                    return BravoWins;
                }

                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not one of the two sides of this match");
            }

            public bool HasPlayer(int playerId) => IndexOf(playerId) >= 0;

            public Side GetSide(int playerId) => RowOf(playerId).Side;

            public int CountRemaining(Side side)
            {
                var count = 0;
                foreach (var row in _rows)
                {
                    if (row.Side == side && row.IsAlive)
                    {
                        count++;
                    }
                }

                return count;
            }

            public int CountDowned(Side side)
            {
                var count = 0;
                foreach (var row in _rows)
                {
                    if (row.Side == side && row.IsDowned)
                    {
                        count++;
                    }
                }

                return count;
            }

            public bool IsDowned(int playerId)
            {
                var index = IndexOf(playerId);
                return index >= 0 && _rows[index].IsDowned;
            }

            public float? GetBleedoutRemaining(int playerId)
            {
                var index = IndexOf(playerId);
                return index >= 0 && _rows[index].IsDowned ? _rows[index].BleedoutSeconds : (float?)null;
            }

            public float GetReviveProgress(int playerId)
            {
                var index = IndexOf(playerId);
                return index < 0 ? 0f : Progress(_rows[index].RevivePercent);
            }

            // 誰に刺されていても返す。HUD は自分の刺している相手 (InteractTargetId) のものだけを出す
            public float GetExecuteProgress(int playerId)
            {
                var index = IndexOf(playerId);
                return index < 0 ? 0f : Progress(_rows[index].ExecutePercent);
            }

            // 百分率で届くので、HUD が切り捨てて同じ百分率に戻るよう、その百分率の幅の真ん中を返す
            private static float Progress(int percent)
            {
                if (percent <= 0)
                {
                    return 0f;
                }

                return percent >= DownedSync.FullPercent ? 1f : (percent + 0.5f) / DownedSync.FullPercent;
            }

            // 秒と百分率は丸めた値で届くので、そのまま比べる
            public bool SameAs(View other)
            {
                if (Phase != other.Phase || RoundNumber != other.RoundNumber || WinsToWin != other.WinsToWin
                    || PreparationRemaining != other.PreparationRemaining || RoundRemaining != other.RoundRemaining
                    || HasFinishedRound != other.HasFinishedRound || LastRoundWinner != other.LastRoundWinner || Winner != other.Winner
                    || AlphaWins != other.AlphaWins || BravoWins != other.BravoWins || _rows.Length != other._rows.Length)
                {
                    return false;
                }

                for (var i = 0; i < _rows.Length; i++)
                {
                    if (!_rows[i].SameAs(other._rows[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            private Row RowOf(int playerId)
            {
                var index = IndexOf(playerId);
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id is in the snapshot");
                }

                return _rows[index];
            }

            private int IndexOf(int playerId)
            {
                for (var i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i].PlayerId == playerId)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }
    }
}
