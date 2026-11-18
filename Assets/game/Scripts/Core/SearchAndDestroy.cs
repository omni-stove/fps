using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 倒れたことは次の Tick で気づくので、その場で爆弾を落とせるよう ReportKill・ReportDeath・RemovePlayer をこのクラスにも置く。
    // 全滅は ModeSession と同じく Tick の頭でまとめて調べる (同じ間の相打ちを報告の順によらず同時に扱うため)。
    public sealed class SearchAndDestroy : ISearchAndDestroyView
    {
        // 体力と位置は Core の外にあるので、受けた側がここで戻す。
        public event Action<int> PreparationStarted;

        public event Action<int> RoundStarted;

        public event Action<Side?, RoundEndReason> RoundEnded;

        public event Action SidesSwapped;

        public event Action<Side> Ended;

        public event Action<int> BombPickedUp;

        public event Action<int> BombDropped;

        public event Action<int, int> BombPlanted;

        public event Action<int> BombDefused;

        public event Action<int> BombDetonated;

        private readonly ModeSession _mode;
        private readonly RoundSession _rounds;

        // 参加者ごとの、長押しを続けている設置地点 (続けていなければ無い)。顔ぶれの変化は Tick で拾う。
        private readonly Dictionary<int, int> _plantIntents = new Dictionary<int, int>();
        private readonly HashSet<int> _defuseIntents = new HashSet<int>();

        // イベントのハンドラで Reset されたら、その後の処理を続けないために数える。
        private int _generation;
        private RoundEndReason? _decidedReason;
        private Side? _decidedWinner;

        public SearchAndDestroySpec Spec { get; }

        public ModeSession Mode => _mode;

        public RoundSession Rounds => _rounds;

        public RoundPhase Phase => _rounds.Phase;

        public int RoundNumber => _rounds.RoundNumber;

        public int WinsToWin => Spec.WinsToWin;

        public float PreparationRemaining => _rounds.PreparationRemaining;

        public Side Attacker => _rounds.Attacker;

        public Side Defender => _rounds.Defender;

        public Side? Winner => _rounds.Winner;

        public BombState Bomb { get; private set; }

        public int? CarrierId { get; private set; }

        public int? PlantedSite { get; private set; }

        public int? PlanterId { get; private set; }

        public int? PlantingSite { get; private set; }

        public float PlantProgress { get; private set; }

        public int? DefuserId { get; private set; }

        public float DefuseProgress { get; private set; }

        public float RoundRemaining { get; private set; }

        public float FuseRemaining { get; private set; }

        public RoundEndReason? LastRoundEnd { get; private set; }

        public Side? LastRoundWinner { get; private set; }

        public float PlantFraction => PlantProgress / Spec.PlantSeconds;

        public float DefuseFraction => DefuseProgress / Spec.DefuseSeconds;

        public SearchAndDestroy(SearchAndDestroySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            // ラウンドの時間は設置で止める必要があり、ModeSession の時間切れでは止められないので、このクラスで数える。
            _mode = new ModeSession(
                new TwoTeams(),
                float.PositiveInfinity,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new BombWin(this) });
            _rounds = new RoundSession(_mode, spec.WinsToWin, spec.PreparationSeconds, spec.SwapEveryRounds);
            _rounds.PreparationStarted += OnPreparationStarted;
            _rounds.RoundStarted += round => RoundStarted?.Invoke(round);
            _rounds.RoundEnded += OnRoundEnded;
            _rounds.SidesSwapped += () => SidesSwapped?.Invoke();
            _rounds.Ended += winner => Ended?.Invoke(winner);
            ResetRoundState();
        }

        public void Start() => _rounds.Start();

        public void Reset()
        {
            _generation++;
            _rounds.Reset();
            ResetRoundState();
            LastRoundEnd = null;
            LastRoundWinner = null;
        }

        public void AddPlayer(int playerId, Side side)
        {
            _mode.AddPlayer(playerId, side);
            if (Phase != RoundPhase.Live && Phase != RoundPhase.Ended && Bomb == BombState.Dropped && side == Attacker)
            {
                AssignBomb();
            }
        }

        public void RemovePlayer(int playerId)
        {
            _mode.RemovePlayer(playerId);
            _plantIntents.Remove(playerId);
            _defuseIntents.Remove(playerId);
            if (CarrierId != playerId)
            {
                return;
            }

            if (Phase == RoundPhase.Live)
            {
                DropFrom(playerId);
            }
            else
            {
                AssignBomb();
            }
        }

        public int GetRoundWins(Side side) => _rounds.GetRoundWins(side);

        public bool HasPlayer(int playerId) => _mode.HasPlayer(playerId);

        public bool IsAttacker(int playerId) => _mode.GetSide(playerId) == Attacker;

        public void ReportKill(int killerId, int victimId)
        {
            _rounds.ReportKill(killerId, victimId);
            DropIfCarrierDown();
        }

        public void ReportDeath(int playerId)
        {
            _rounds.ReportDeath(playerId);
            DropIfCarrierDown();
        }

        public void SetPlanting(int playerId, int? site)
        {
            _mode.GetSide(playerId);
            if (site.HasValue && (site.Value < 0 || site.Value >= Spec.SiteCount))
            {
                throw new ArgumentOutOfRangeException(nameof(site), site, "no bomb site with this index");
            }

            if (site.HasValue)
            {
                _plantIntents[playerId] = site.Value;
            }
            else
            {
                _plantIntents.Remove(playerId);
            }
        }

        public void SetDefusing(int playerId, bool defusing)
        {
            _mode.GetSide(playerId);
            if (defusing)
            {
                _defuseIntents.Add(playerId);
            }
            else
            {
                _defuseIntents.Remove(playerId);
            }
        }

        public bool PickUpBomb(int playerId)
        {
            var side = _mode.GetSide(playerId);
            if (Phase != RoundPhase.Live
                || _mode.State != GameModeState.Playing
                || Bomb != BombState.Dropped
                || side != Attacker
                || _mode.IsDown(playerId))
            {
                return false;
            }

            Bomb = BombState.Carried;
            CarrierId = playerId;
            BombPickedUp?.Invoke(playerId);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (Phase == RoundPhase.Live && _mode.State == GameModeState.Playing)
            {
                CheckStanding();
            }

            if (Phase == RoundPhase.Live && _mode.State == GameModeState.Playing && !_decidedReason.HasValue)
            {
                var generation = _generation;
                AdvanceObjective(deltaSeconds);
                if (generation != _generation)
                {
                    return;
                }
            }

            _rounds.Tick(deltaSeconds);
        }

        private void OnPreparationStarted(int round)
        {
            ResetRoundState();
            PreparationStarted?.Invoke(round);
        }

        private void OnRoundEnded(Side? winner)
        {
            LastRoundEnd = _decidedReason ?? (winner.HasValue
                ? (winner.Value == Attacker ? RoundEndReason.DefendersEliminated : RoundEndReason.AttackersEliminated)
                : RoundEndReason.BothEliminated);
            LastRoundWinner = winner;
            RoundEnded?.Invoke(winner, LastRoundEnd.Value);
        }

        private void ResetRoundState()
        {
            _plantIntents.Clear();
            _defuseIntents.Clear();
            _decidedReason = null;
            _decidedWinner = null;
            PlantedSite = null;
            PlanterId = null;
            PlantingSite = null;
            PlantProgress = 0f;
            DefuserId = null;
            DefuseProgress = 0f;
            RoundRemaining = Spec.RoundSeconds;
            FuseRemaining = Spec.FuseSeconds;
            AssignBomb();
        }

        private void AssignBomb()
        {
            int? carrier = null;
            foreach (var playerId in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(playerId) == Attacker)
                {
                    carrier = playerId;
                    break;
                }
            }

            CarrierId = carrier;
            Bomb = carrier.HasValue ? BombState.Carried : BombState.Dropped;
        }

        private void DropIfCarrierDown()
        {
            if (Phase != RoundPhase.Live || Bomb != BombState.Carried || !CarrierId.HasValue)
            {
                return;
            }

            var carrier = CarrierId.Value;
            if (!_mode.HasPlayer(carrier) || _mode.IsDown(carrier) || _mode.GetSide(carrier) != Attacker)
            {
                DropFrom(carrier);
            }
        }

        private void DropFrom(int carrierId)
        {
            Bomb = BombState.Dropped;
            CarrierId = null;
            PlantingSite = null;
            PlantProgress = 0f;
            BombDropped?.Invoke(carrierId);
        }

        private void AdvanceObjective(float deltaSeconds)
        {
            var generation = _generation;
            DropIfCarrierDown();
            if (generation != _generation)
            {
                return;
            }

            if (Bomb == BombState.Planted)
            {
                AdvanceFuse(deltaSeconds);
                return;
            }

            var site = PlantCandidate();
            if (site.HasValue)
            {
                if (PlantingSite != site)
                {
                    PlantingSite = site;
                    PlantProgress = 0f;
                }

                var needed = Spec.PlantSeconds - PlantProgress;
                if (needed <= deltaSeconds && needed <= RoundRemaining)
                {
                    RoundRemaining -= needed;
                    Plant(CarrierId.Value, site.Value);
                    if (generation != _generation)
                    {
                        return;
                    }

                    AdvanceFuse(deltaSeconds - needed);
                    return;
                }

                PlantProgress += Math.Min(deltaSeconds, RoundRemaining);
            }
            else
            {
                PlantingSite = null;
                PlantProgress = 0f;
            }

            RoundRemaining -= deltaSeconds;
            if (RoundRemaining <= 0f)
            {
                RoundRemaining = 0f;
                PlantingSite = null;
                PlantProgress = 0f;
                Decide(Defender, RoundEndReason.TimeUp);
            }
        }

        private void AdvanceFuse(float deltaSeconds)
        {
            var defuser = FirstDefuser();
            if (defuser.HasValue)
            {
                DefuserId = defuser;
                var needed = Spec.DefuseSeconds - DefuseProgress;
                if (needed <= deltaSeconds && needed <= FuseRemaining)
                {
                    FuseRemaining -= needed;
                    DefuseProgress = Spec.DefuseSeconds;
                    Bomb = BombState.Defused;
                    Decide(Defender, RoundEndReason.Defused);
                    BombDefused?.Invoke(defuser.Value);
                    return;
                }

                DefuseProgress += Math.Min(deltaSeconds, FuseRemaining);
            }
            else
            {
                DefuserId = null;
                DefuseProgress = 0f;
            }

            FuseRemaining -= deltaSeconds;
            if (FuseRemaining <= 0f)
            {
                FuseRemaining = 0f;
                Bomb = BombState.Detonated;
                Decide(Attacker, RoundEndReason.Detonated);
                BombDetonated?.Invoke(PlantedSite.Value);
            }
        }

        private void Plant(int playerId, int site)
        {
            Bomb = BombState.Planted;
            CarrierId = null;
            PlantedSite = site;
            PlanterId = playerId;
            PlantingSite = null;
            PlantProgress = 0f;
            FuseRemaining = Spec.FuseSeconds;
            DefuseProgress = 0f;
            DefuserId = null;
            BombPlanted?.Invoke(playerId, site);
        }

        private int? PlantCandidate()
        {
            if (Bomb != BombState.Carried || !CarrierId.HasValue)
            {
                return null;
            }

            var carrier = CarrierId.Value;
            if (!_plantIntents.TryGetValue(carrier, out var site) || _mode.IsDown(carrier))
            {
                return null;
            }

            return site;
        }

        private int? FirstDefuser()
        {
            int? first = null;
            foreach (var playerId in _defuseIntents)
            {
                if (!_mode.HasPlayer(playerId) || _mode.IsDown(playerId) || _mode.GetSide(playerId) != Defender)
                {
                    continue;
                }

                if (!first.HasValue || playerId < first.Value)
                {
                    first = playerId;
                }
            }

            return first;
        }

        private void Decide(Side? winner, RoundEndReason reason)
        {
            _decidedWinner = winner;
            _decidedReason = reason;
        }

        private ModeOutcome CheckStanding()
        {
            if (_decidedReason.HasValue)
            {
                return ModeOutcome.Decided(_decidedWinner);
            }

            var attackersOut = _mode.IsSideEliminated(Attacker);
            var defendersOut = _mode.IsSideEliminated(Defender);
            var planted = Bomb == BombState.Planted;
            if (defendersOut)
            {
                if (attackersOut && !planted)
                {
                    Decide(null, RoundEndReason.BothEliminated);
                }
                else
                {
                    Decide(Attacker, RoundEndReason.DefendersEliminated);
                }
            }
            else if (attackersOut && !planted)
            {
                Decide(Defender, RoundEndReason.AttackersEliminated);
            }
            else
            {
                return ModeOutcome.Undecided;
            }

            return ModeOutcome.Decided(_decidedWinner);
        }

        // 時間切れは SearchAndDestroy が数えるので、ここでは扱わない。
        private sealed class BombWin : IWinCondition
        {
            private readonly SearchAndDestroy _owner;

            public BombWin(SearchAndDestroy owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => _owner.CheckStanding();

            public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
        }
    }
}
