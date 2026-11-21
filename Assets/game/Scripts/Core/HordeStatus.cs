using System;

namespace Shooter.Core
{
    // オフラインは Horde を渡して今の値を読ませ、オンライン (ClientHordeStatus) はスナップショットで届いた値を渡す。
    // どちらも同じ型にするのは、UI が HordeStatus に登録した書式役を 1 つのまま使えるようにするため。
    public sealed class HordeStatus
    {
        private readonly int _waveNumber;
        private readonly int? _waveCount;
        private readonly WavePhase _phase;
        private readonly int _remainingEnemies;
        private readonly float _restRemaining;
        private readonly int _currency;
        private readonly bool _isEnded;
        private readonly bool _isVictory;
        private readonly double _elapsedSeconds;
        private readonly bool _awaitingReturn;

        public HordeStatus(Horde horde, int? localPlayerId, HordeFortificationOffer? offer)
        {
            Horde = horde ?? throw new ArgumentNullException(nameof(horde));
            LocalPlayerId = localPlayerId;
            Offer = localPlayerId.HasValue ? offer : null;
        }

        public HordeStatus(
            int waveNumber,
            int? waveCount,
            WavePhase phase,
            int remainingEnemies,
            float restRemaining,
            int currency,
            bool isEnded,
            bool isVictory,
            double elapsedSeconds,
            int? localPlayerId,
            bool awaitingReturn,
            HordeFortificationOffer? offer = null)
        {
            if (waveNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber), waveNumber, "waveNumber must not be negative");
            }

            if (waveCount.HasValue && waveCount.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(waveCount), waveCount, "waveCount must be at least 1, or null when endless");
            }

            if (!(restRemaining >= 0f) || float.IsInfinity(restRemaining))
            {
                throw new ArgumentOutOfRangeException(nameof(restRemaining), restRemaining, "restRemaining must be non-negative and finite");
            }

            if (!(elapsedSeconds >= 0d) || double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "elapsedSeconds must be non-negative and finite");
            }

            _waveNumber = waveNumber;
            _waveCount = waveCount;
            _phase = phase;
            _remainingEnemies = Math.Max(0, remainingEnemies);
            _restRemaining = restRemaining;
            _currency = Math.Max(0, currency);
            _isEnded = isEnded;
            _isVictory = isEnded && isVictory;
            _elapsedSeconds = elapsedSeconds;
            LocalPlayerId = localPlayerId;
            _awaitingReturn = localPlayerId.HasValue && awaitingReturn;
            Offer = localPlayerId.HasValue ? offer : null;
        }

        // スナップショットから作ったときは null
        public Horde Horde { get; }

        public int? LocalPlayerId { get; }

        public HordeFortificationOffer? Offer { get; }

        public int WaveNumber => Horde?.Schedule.WaveNumber ?? _waveNumber;

        public int? WaveCount => Horde != null ? Horde.Schedule.WaveCount : _waveCount;

        public WavePhase Phase => Horde?.Schedule.Phase ?? _phase;

        public int RemainingEnemies => Horde?.Schedule.RemainingEnemies ?? _remainingEnemies;

        public float RestRemaining => Horde?.Schedule.RestRemaining ?? _restRemaining;

        public int Currency => Horde?.Currency ?? _currency;

        public bool IsEnded => Horde != null ? Horde.Session.State == GameModeState.Ended : _isEnded;

        public bool IsVictory => Horde?.IsVictory ?? _isVictory;

        public double ElapsedSeconds => Horde?.Session.ElapsedSeconds ?? _elapsedSeconds;

        // 倒れていて、今の波が終わるまで戻らないか
        public bool IsAwaitingReturn
        {
            get
            {
                if (Horde == null)
                {
                    return _awaitingReturn;
                }

                var session = Horde.Session;
                return LocalPlayerId.HasValue && session.HasPlayer(LocalPlayerId.Value)
                    && session.IsAwaitingRespawn(LocalPlayerId.Value) && !Horde.IsBetweenWaves;
            }
        }
    }
}
