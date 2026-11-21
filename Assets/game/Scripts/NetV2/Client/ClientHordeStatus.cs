using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に Core の HordeStatus を置くのは、オフラインの HordeRunner と同じ型にして、UI が HordeStatus に登録した書式役をそのまま使えるようにするため。
    // サーバー (HordeRules) は守る側を Alpha に決め、既存の欄に載せて送る: 通貨は AlphaPoints、今の波の残りの敵は BravoPoints、
    // 波の番号と数は RoundNumber・RoundsToWin (0 なら終わりが無い)、休憩の残りは NextWaveSeconds、始まってからの秒は ObjectiveRemainingSeconds。
    // 波の段階は送られないので、これらの値から決める。
    // 直す・置く案内 (HordeStatus.Offer) は防衛物の場所も部品の重なりも手元に無いので、サーバーが持ち主の部分 (OwnerSnapshot.HordeOffer) で送ったものをそのまま出す
    public sealed class ClientHordeStatus
    {
        private static readonly Side _defenders = Side.Alpha;

        private readonly int _localPlayerId;
        private readonly Func<IMessage, bool> _send;
        private readonly HordeFortifyRequestMessage _request = new HordeFortifyRequestMessage();
        private Shown _shown;

        // send が null なら案内を出すだけで、RequestFortify は送らずに false を返す
        public ClientHordeStatus(int localPlayerId, Func<IMessage, bool> send = null)
        {
            _localPlayerId = localPlayerId;
            _send = send;
        }

        // Horde の試合のスナップショットが届いていなければ null
        public ModeStatus Status { get; private set; }

        public HordeStatus Horde => Status?.Detail as HordeStatus;

        // 案内に添える操作の表示 (オフラインの HordeRunner.InteractKeyLabel と同じ)。変えたら次の Apply で案内を作り直す
        public string InteractKeyLabel { get; set; }

        // オフラインの HordeRunner.OffersInteract と同じく、今押せば払えるときだけ true
        public bool OffersFortify => Horde?.Offer?.Verdict == HordePurchaseVerdict.Ok;

        // 押されたときに呼ぶ。払えないと分かっている (案内が無い・足りない・波の最中) なら送らない。
        // 払えたかはサーバーが次のスナップショットの通貨と案内で知らせる
        public bool RequestFortify()
        {
            if (_send == null || !OffersFortify)
            {
                return false;
            }

            return _send(_request);
        }

        // Status を作り直したら true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.Horde)
            {
                return Clear();
            }

            var waveNumber = Math.Max(0, match.RoundNumber);
            int? waveCount = match.RoundsToWin > 0 ? match.RoundsToWin : (int?)null;
            var remaining = Math.Max(0, match.BravoPoints);
            var ended = match.State == GameModeState.Ended;
            var winner = ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null;
            var phase = PhaseOf(waveNumber, waveCount, remaining, winner);
            var own = FindOwnRow(snapshot, out var ownTeam, out var ownHealth);
            var awaitingReturn = own && ownTeam == Team.Alpha && ownHealth <= 0 && !ended && phase == WavePhase.Fighting;
            var offer = own && !ended && snapshot.HasOwner ? snapshot.Owner.HordeOffer : null;
            var shown = new Shown(
                waveNumber,
                waveCount,
                phase,
                remaining,
                phase == WavePhase.Resting ? Math.Max(0, match.NextWaveSeconds) : 0,
                Math.Max(0, match.AlphaPoints),
                Math.Max(0, match.AlphaScore),
                Math.Max(0, match.ObjectiveRemainingSeconds),
                own ? Side.FromTeam(ownTeam) : (Side?)null,
                RespawnSeconds(snapshot),
                awaitingReturn,
                ended,
                winner,
                offer,
                offer.HasValue ? InteractKeyLabel ?? string.Empty : string.Empty);
            if (Status != null && shown.Equals(_shown))
            {
                return false;
            }

            _shown = shown;
            Status = new ModeStatus(
                null,
                new[] { new SideScore(_defenders, shown.Score) },
                shown.OwnSide,
                shown.RespawnSeconds,
                shown.IsEnded,
                shown.Winner,
                new HordeStatus(
                    shown.WaveNumber,
                    shown.WaveCount,
                    shown.Phase,
                    shown.RemainingEnemies,
                    shown.RestSeconds,
                    shown.Currency,
                    shown.IsEnded,
                    shown.Winner == _defenders,
                    shown.ElapsedSeconds,
                    own ? _localPlayerId : (int?)null,
                    shown.IsAwaitingReturn,
                    OfferOf(shown.Offer, shown.KeyLabel)));
            return true;
        }

        private static HordeFortificationOffer? OfferOf(HordeOfferSync? offer, string keyLabel)
        {
            if (!offer.HasValue)
            {
                return null;
            }

            var value = offer.Value;
            return new HordeFortificationOffer(value.Action, value.NameKey, value.Cost, value.Verdict, keyLabel);
        }

        // 試合を離れたときや、Horde でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            if (Status == null)
            {
                return false;
            }

            Status = null;
            _shown = default;
            return true;
        }

        // 最後の波を全滅させた後に試合の終わりが届くまでの間も、休憩でなく終わりとして見せる
        public static WavePhase PhaseOf(int waveNumber, int? waveCount, int remainingEnemies, Side? winner)
        {
            if (winner == _defenders)
            {
                return WavePhase.Completed;
            }

            if (waveNumber == 0)
            {
                return WavePhase.Waiting;
            }

            if (remainingEnemies > 0)
            {
                return WavePhase.Fighting;
            }

            return waveCount.HasValue && waveNumber >= waveCount.Value ? WavePhase.Completed : WavePhase.Resting;
        }

        private bool FindOwnRow(SnapshotMessage snapshot, out Team team, out int health)
        {
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    team = row.Team;
                    health = row.Health;
                    return true;
                }
            }

            team = default;
            health = 0;
            return false;
        }

        private static float? RespawnSeconds(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner || !snapshot.Owner.RespawnSeconds.HasValue)
            {
                return null;
            }

            return Math.Max(0, snapshot.Owner.RespawnSeconds.Value);
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int waveNumber,
                int? waveCount,
                WavePhase phase,
                int remainingEnemies,
                int restSeconds,
                int currency,
                int score,
                int elapsedSeconds,
                Side? ownSide,
                float? respawnSeconds,
                bool isAwaitingReturn,
                bool isEnded,
                Side? winner,
                HordeOfferSync? offer,
                string keyLabel)
            {
                WaveNumber = waveNumber;
                WaveCount = waveCount;
                Phase = phase;
                RemainingEnemies = remainingEnemies;
                RestSeconds = restSeconds;
                Currency = currency;
                Score = score;
                ElapsedSeconds = elapsedSeconds;
                OwnSide = ownSide;
                RespawnSeconds = respawnSeconds;
                IsAwaitingReturn = isAwaitingReturn;
                IsEnded = isEnded;
                Winner = winner;
                Offer = offer;
                KeyLabel = keyLabel;
            }

            public int WaveNumber { get; }

            public int? WaveCount { get; }

            public WavePhase Phase { get; }

            public int RemainingEnemies { get; }

            public int RestSeconds { get; }

            public int Currency { get; }

            public int Score { get; }

            public int ElapsedSeconds { get; }

            public Side? OwnSide { get; }

            public float? RespawnSeconds { get; }

            public bool IsAwaitingReturn { get; }

            public bool IsEnded { get; }

            public Side? Winner { get; }

            public HordeOfferSync? Offer { get; }

            public string KeyLabel { get; }

            public bool Equals(Shown other)
            {
                return WaveNumber == other.WaveNumber
                    && WaveCount == other.WaveCount
                    && Phase == other.Phase
                    && RemainingEnemies == other.RemainingEnemies
                    && RestSeconds == other.RestSeconds
                    && Currency == other.Currency
                    && Score == other.Score
                    && ElapsedSeconds == other.ElapsedSeconds
                    && OwnSide == other.OwnSide
                    && RespawnSeconds == other.RespawnSeconds
                    && IsAwaitingReturn == other.IsAwaitingReturn
                    && IsEnded == other.IsEnded
                    && Winner == other.Winner
                    && Offer == other.Offer
                    && string.Equals(KeyLabel, other.KeyLabel, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(WaveNumber, Phase, RemainingEnemies, Currency, ElapsedSeconds);
        }
    }
}
