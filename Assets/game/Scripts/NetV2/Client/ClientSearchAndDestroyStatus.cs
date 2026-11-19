using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いたスナップショットから、オフラインの SearchAndDestroyRunner と同じ形の ModeStatus (Detail は SearchAndDestroyStatus) を作る。
    // ラウンドの番号・勝ちに要るラウンド数・前のラウンドの結果・設置と解除の進み具合はサーバーが送る値をそのまま出す
    public sealed class ClientSearchAndDestroyStatus
    {
        private readonly int _localPlayerId;
        private readonly List<KeyValuePair<int, Team>> _players = new List<KeyValuePair<int, Team>>();
        private Side _attacker = Side.Alpha;
        private View _view;

        public ClientSearchAndDestroyStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // 爆弾設置の試合でなければ null。変わるたびに別のものになる
        public ModeStatus Status { get; private set; }

        // Status が変わったら true
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.SearchAndDestroy)
            {
                return Reset();
            }

            var phase = match.HasRound ? match.Round : RoundPhase.Waiting;
            var bomb = match.HasBomb ? match.Bomb : BombState.Carried;
            var plantedOrAfter = bomb == BombState.Planted || bomb == BombState.Defused || bomb == BombState.Detonated;
            // 設置された爆弾の地点が無いスナップショットは HUD に出せないので、前の状態を残す
            if (match.HasBomb && plantedOrAfter && !match.HasActivePoint)
            {
                return false;
            }

            var bombItem = FindBomb(snapshot.ObjectiveItems);
            if (bombItem >= 0)
            {
                _attacker = Side.FromTeam(snapshot.ObjectiveItems[bombItem].Team);
            }

            var plantingSite = match.HasBomb && bomb == BombState.Carried && match.HasActivePoint ? match.ActivePointIndex : (int?)null;
            var defuserId = bomb == BombState.Planted && match.HasDefuser ? match.DefuserId : (int?)null;

            _players.Clear();
            Side? ownSide = null;
            var localPresent = false;
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var row = snapshot.Players[i];
                _players.Add(new KeyValuePair<int, Team>(row.PlayerId, row.Team));
                if (row.PlayerId == _localPlayerId)
                {
                    ownSide = Side.FromTeam(row.Team);
                    localPresent = true;
                }
            }

            var remaining = Math.Max(0, match.RemainingSeconds);
            var carrier = bombItem >= 0 && snapshot.ObjectiveItems[bombItem].HasCarrier
                ? snapshot.ObjectiveItems[bombItem].CarrierId
                : (int?)null;
            var ended = match.State == GameModeState.Ended;
            var view = new View(
                phase,
                Math.Max(0, match.RoundNumber),
                Math.Max(0, match.RoundsToWin),
                _attacker,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null,
                bomb,
                carrier,
                plantingSite,
                plantingSite.HasValue ? match.PlantFraction : 0f,
                plantedOrAfter ? match.ActivePointIndex : (int?)null,
                defuserId,
                defuserId.HasValue ? match.DefuseFraction : 0f,
                phase == RoundPhase.Preparing ? remaining : 0f,
                phase == RoundPhase.Live && !plantedOrAfter ? remaining : 0f,
                plantedOrAfter ? Math.Max(0, match.ObjectiveRemainingSeconds) : 0f,
                match.HasLastRound ? match.LastRoundEnd : (RoundEndReason?)null,
                match.HasLastRound && match.HasLastRoundWinner ? Side.FromTeam(match.LastRoundWinner) : (Side?)null,
                Math.Max(0, match.AlphaPoints),
                Math.Max(0, match.BravoPoints),
                _players.ToArray());

            if (Status != null && _view != null && _view.SameAs(view) && Status.OwnSide == ownSide && Status.IsEnded == ended
                && Status.RemainingSeconds == remaining && (Status.Detail as SearchAndDestroyStatus)?.LocalPlayerId == (localPresent ? _localPlayerId : (int?)null))
            {
                return false;
            }

            _view = view;
            Status = new ModeStatus(
                remaining,
                new[] { new SideScore(Side.Alpha, view.AlphaWins), new SideScore(Side.Bravo, view.BravoWins) },
                ownSide,
                null,
                ended,
                view.Winner,
                new SearchAndDestroyStatus(view, localPresent ? _localPlayerId : (int?)null));
            return true;
        }

        // Status を捨てたら true
        public bool Reset()
        {
            var had = Status != null;
            Status = null;
            _view = null;
            _attacker = Side.Alpha;
            _players.Clear();
            return had;
        }

        private static int FindBomb(SnapshotList<ObjectiveItemSnapshot> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Kind == ObjectiveItemKind.Bomb)
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class View : ISearchAndDestroyView
        {
            private readonly KeyValuePair<int, Team>[] _players;

            public View(
                RoundPhase phase,
                int roundNumber,
                int winsToWin,
                Side attacker,
                Side? winner,
                BombState bomb,
                int? carrierId,
                int? plantingSite,
                float plantFraction,
                int? plantedSite,
                int? defuserId,
                float defuseFraction,
                float preparationRemaining,
                float roundRemaining,
                float fuseRemaining,
                RoundEndReason? lastRoundEnd,
                Side? lastRoundWinner,
                int alphaWins,
                int bravoWins,
                KeyValuePair<int, Team>[] players)
            {
                Phase = phase;
                RoundNumber = roundNumber;
                WinsToWin = winsToWin;
                Attacker = attacker;
                Defender = attacker == Side.Alpha ? Side.Bravo : Side.Alpha;
                Winner = winner;
                Bomb = bomb;
                CarrierId = carrierId;
                PlantingSite = plantingSite;
                PlantFraction = plantFraction;
                PlantedSite = plantedSite;
                DefuserId = defuserId;
                DefuseFraction = defuseFraction;
                PreparationRemaining = preparationRemaining;
                RoundRemaining = roundRemaining;
                FuseRemaining = fuseRemaining;
                LastRoundEnd = lastRoundEnd;
                LastRoundWinner = lastRoundWinner;
                AlphaWins = alphaWins;
                BravoWins = bravoWins;
                _players = players;
            }

            public RoundPhase Phase { get; }

            public int RoundNumber { get; }

            public int WinsToWin { get; }

            public float PreparationRemaining { get; }

            public Side Attacker { get; }

            public Side Defender { get; }

            public Side? Winner { get; }

            public BombState Bomb { get; }

            public int? CarrierId { get; }

            public int? PlantingSite { get; }

            public float PlantFraction { get; }

            public int? PlantedSite { get; }

            public int? DefuserId { get; }

            public float DefuseFraction { get; }

            public float RoundRemaining { get; }

            // 設置の前は HUD に出ないので 0
            public float FuseRemaining { get; }

            public RoundEndReason? LastRoundEnd { get; }

            public Side? LastRoundWinner { get; }

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

            public bool HasPlayer(int playerId) => TryGetTeam(playerId, out _);

            public bool IsAttacker(int playerId)
            {
                if (!TryGetTeam(playerId, out var team))
                {
                    throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id is in the snapshot");
                }

                return Side.FromTeam(team) == Attacker;
            }

            // 進み具合は毎 tick 届くので、HUD に出る百分率が同じ間は同じとみなして ModeStatus を出し直さない
            public bool SameAs(View other)
            {
                if (Phase != other.Phase || RoundNumber != other.RoundNumber || WinsToWin != other.WinsToWin || Attacker != other.Attacker
                    || Winner != other.Winner || Bomb != other.Bomb || CarrierId != other.CarrierId || PlantingSite != other.PlantingSite
                    || PercentOf(PlantFraction) != PercentOf(other.PlantFraction) || PlantedSite != other.PlantedSite
                    || DefuserId != other.DefuserId || PercentOf(DefuseFraction) != PercentOf(other.DefuseFraction)
                    || PreparationRemaining != other.PreparationRemaining
                    || RoundRemaining != other.RoundRemaining || FuseRemaining != other.FuseRemaining || LastRoundEnd != other.LastRoundEnd
                    || LastRoundWinner != other.LastRoundWinner || AlphaWins != other.AlphaWins || BravoWins != other.BravoWins
                    || _players.Length != other._players.Length)
                {
                    return false;
                }

                for (var i = 0; i < _players.Length; i++)
                {
                    if (_players[i].Key != other._players[i].Key || _players[i].Value != other._players[i].Value)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static int PercentOf(float fraction) => (int)Math.Floor(Math.Min(1f, Math.Max(0f, fraction)) * 100f);

            private bool TryGetTeam(int playerId, out Team team)
            {
                foreach (var pair in _players)
                {
                    if (pair.Key == playerId)
                    {
                        team = pair.Value;
                        return true;
                    }
                }

                team = Team.Alpha;
                return false;
            }
        }
    }
}
