using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いたスナップショットから、オフラインの SearchAndDestroyRunner と同じ形の ModeStatus (Detail は SearchAndDestroyStatus) を作る。
    // スナップショットに無い値 (勝ちに要るラウンド数・設置にかかる秒数) は、受け取った Spec の値を使う。
    // 設置の進み具合は送られないので、同じ地点で設置が続いている間の経過を手元で数える。
    // 解除している人・ラウンドの終わり方は送られないので、写しでは無いものとして出す
    public sealed class ClientSearchAndDestroyStatus
    {
        private readonly int _localPlayerId;
        private readonly SearchAndDestroySpec _spec;
        private readonly List<KeyValuePair<int, Team>> _players = new List<KeyValuePair<int, Team>>();
        private Side _attacker = Side.Alpha;
        private int _alphaWins;
        private int _bravoWins;
        private Side? _lastRoundWinner;
        private int? _plantingSite;
        private float _plantElapsed;
        private View _view;

        public ClientSearchAndDestroyStatus(int localPlayerId, SearchAndDestroySpec spec)
        {
            _localPlayerId = localPlayerId;
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
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

            TrackRoundWinner(match.AlphaPoints, match.BravoPoints);

            var plantingSite = match.HasBomb && bomb == BombState.Carried && match.HasActivePoint ? match.ActivePointIndex : (int?)null;
            if (plantingSite != _plantingSite)
            {
                _plantingSite = plantingSite;
                _plantElapsed = 0f;
            }

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
                this,
                phase,
                _attacker,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null,
                bomb,
                carrier,
                plantingSite,
                plantedOrAfter ? match.ActivePointIndex : (int?)null,
                phase == RoundPhase.Preparing ? remaining : 0f,
                phase == RoundPhase.Live && !plantedOrAfter ? remaining : 0f,
                plantedOrAfter ? Math.Max(0, match.ObjectiveRemainingSeconds) : _spec.FuseSeconds,
                Math.Max(0, match.AlphaPoints),
                Math.Max(0, match.BravoPoints),
                _lastRoundWinner,
                _players.ToArray(),
                plantingSite.HasValue ? Math.Min(1f, _plantElapsed / _spec.PlantSeconds) : 0f);

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

        // 設置の進み具合を進める。表示の割合が変わって Status を作り直したら true
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds) || _view == null || !_plantingSite.HasValue)
            {
                return false;
            }

            var before = PercentOf(_plantElapsed);
            _plantElapsed = Math.Min(_spec.PlantSeconds, _plantElapsed + deltaSeconds);
            if (PercentOf(_plantElapsed) == before)
            {
                return false;
            }

            var status = Status;
            _view = _view.WithPlantFraction(_plantElapsed / _spec.PlantSeconds);
            var detail = (SearchAndDestroyStatus)status.Detail;
            Status = new ModeStatus(
                status.RemainingSeconds,
                status.SideScores,
                status.OwnSide,
                status.RespawnSeconds,
                status.IsEnded,
                status.Winner,
                new SearchAndDestroyStatus(_view, detail.LocalPlayerId));
            return true;
        }

        // Status を捨てたら true
        public bool Reset()
        {
            var had = Status != null;
            Status = null;
            _view = null;
            _attacker = Side.Alpha;
            _alphaWins = 0;
            _bravoWins = 0;
            _lastRoundWinner = null;
            _plantingSite = null;
            _plantElapsed = 0f;
            _players.Clear();
            return had;
        }

        private int PercentOf(float elapsed) => (int)Math.Floor(Math.Min(1f, elapsed / _spec.PlantSeconds) * 100f);

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

        // 勝ち数が増えた側を前のラウンドの勝者にする。両方が減ったら次の試合が始まったので忘れる
        private void TrackRoundWinner(int alpha, int bravo)
        {
            if (alpha < _alphaWins || bravo < _bravoWins)
            {
                _lastRoundWinner = null;
            }
            else if (alpha > _alphaWins)
            {
                _lastRoundWinner = Side.Alpha;
            }
            else if (bravo > _bravoWins)
            {
                _lastRoundWinner = Side.Bravo;
            }

            _alphaWins = alpha;
            _bravoWins = bravo;
        }

        private sealed class View : ISearchAndDestroyView
        {
            private readonly ClientSearchAndDestroyStatus _owner;
            private readonly KeyValuePair<int, Team>[] _players;

            public View(
                ClientSearchAndDestroyStatus owner,
                RoundPhase phase,
                Side attacker,
                Side? winner,
                BombState bomb,
                int? carrierId,
                int? plantingSite,
                int? plantedSite,
                float preparationRemaining,
                float roundRemaining,
                float fuseRemaining,
                int alphaWins,
                int bravoWins,
                Side? lastRoundWinner,
                KeyValuePair<int, Team>[] players,
                float plantFraction = 0f)
            {
                _owner = owner;
                Phase = phase;
                Attacker = attacker;
                Defender = attacker == Side.Alpha ? Side.Bravo : Side.Alpha;
                Winner = winner;
                Bomb = bomb;
                CarrierId = carrierId;
                PlantingSite = plantingSite;
                PlantedSite = plantedSite;
                PreparationRemaining = preparationRemaining;
                RoundRemaining = roundRemaining;
                FuseRemaining = fuseRemaining;
                AlphaWins = alphaWins;
                BravoWins = bravoWins;
                LastRoundWinner = lastRoundWinner;
                _players = players;
                PlantFraction = plantFraction;
            }

            public RoundPhase Phase { get; }

            // スナップショットにラウンドの番号が無いので、勝ち数の和から数える。引き分けのラウンドは数えられない
            public int RoundNumber
            {
                get
                {
                    if (Phase == RoundPhase.Waiting)
                    {
                        return 0;
                    }

                    var played = AlphaWins + BravoWins;
                    return Phase == RoundPhase.Ended ? Math.Max(1, played) : played + 1;
                }
            }

            public int WinsToWin => _owner._spec.WinsToWin;

            public float PreparationRemaining { get; }

            public Side Attacker { get; }

            public Side Defender { get; }

            public Side? Winner { get; }

            public BombState Bomb { get; }

            public int? CarrierId { get; }

            public int? PlantingSite { get; }

            public float PlantFraction { get; }

            public int? PlantedSite { get; }

            public int? DefuserId => null;

            public float DefuseFraction => 0f;

            public float RoundRemaining { get; }

            public float FuseRemaining { get; }

            public RoundEndReason? LastRoundEnd => null;

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

            public View WithPlantFraction(float fraction)
            {
                return new View(
                    _owner, Phase, Attacker, Winner, Bomb, CarrierId, PlantingSite, PlantedSite, PreparationRemaining, RoundRemaining,
                    FuseRemaining, AlphaWins, BravoWins, LastRoundWinner, _players, fraction);
            }

            // 設置の進み具合は Tick で数えるので比べない
            public bool SameAs(View other)
            {
                if (Phase != other.Phase || Attacker != other.Attacker || Winner != other.Winner || Bomb != other.Bomb
                    || CarrierId != other.CarrierId || PlantingSite != other.PlantingSite || PlantedSite != other.PlantedSite
                    || PreparationRemaining != other.PreparationRemaining || RoundRemaining != other.RoundRemaining
                    || FuseRemaining != other.FuseRemaining || AlphaWins != other.AlphaWins || BravoWins != other.BravoWins
                    || LastRoundWinner != other.LastRoundWinner || _players.Length != other._players.Length)
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
